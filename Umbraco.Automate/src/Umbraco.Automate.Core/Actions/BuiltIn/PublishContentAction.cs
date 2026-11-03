using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that publishes a content item in Umbraco CMS.
/// </summary>
[Action("umbracoAutomate.publishContent", "Publish Content",
    Description = "Publishes a content item in Umbraco CMS.",
    Group = "Content",
    Icon = "icon-globe",
    RequiredSections = [UmbracoConstants.Applications.Content],
    RequiredPermissions = [ActionPublish.ActionLetter])]
public sealed class PublishContentAction : ActionBase<PublishContentSettings, PublishContentOutput>, ICmsAction
{
    private readonly IContentPublishingService _contentPublishingService;
    private readonly IUserService _userService;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<PublishContentAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishContentAction"/> class.
    /// </summary>
    public PublishContentAction(
        ActionInfrastructure infrastructure,
        IContentPublishingService contentPublishingService,
        IUserService userService,
        PropertyEditorCollection propertyEditors,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        ILogger<PublishContentAction> logger)
        : base(infrastructure)
    {
        _contentPublishingService = contentPublishingService;
        _userService = userService;
        _propertyEditors = propertyEditors;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PublishContentSettings>();

        if (string.IsNullOrWhiteSpace(settings.ContentKey) || !Guid.TryParse(settings.ContentKey, out var contentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing content key: '{settings.ContentKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (await _authorizer.AuthorizeContentOrFailAsync(contentKey, RequiredPermissions, cancellationToken) is { } failure)
        {
            return failure;
        }

        var culturesToPublish = ParseCultures(settings.Cultures);

        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Publishing content {ContentKey} for cultures [{Cultures}]",
            context.AutomationId, context.RunId, contentKey,
            culturesToPublish.Count == 0 ? "invariant" : string.Join(", ", culturesToPublish.Select(c => c.Culture ?? "*")));

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. The publish raises notifications (e.g. webhook delivery) that resolve
        // content URLs via UrlProvider, which requires an UmbracoContext.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = await _contentPublishingService.PublishAsync(contentKey, culturesToPublish, userKey);

        var item = ActionLogFormat.Item(result.Result?.Content?.Name, contentKey);
        var cultures = ActionLogFormat.Cultures(culturesToPublish.Select(c => c.Culture));

        if (result.Success)
        {
            context.LogInfo(cultures.Length == 0 ? $"Published {item}" : $"Published {item} in {cultures}");

            await WarnAboutUnpublishedSharedPropertiesAsync(context, result.Result?.Content, userKey, culturesToPublish, item);

            return Success(new PublishContentOutput
            {
                ContentKey = contentKey,
                Cultures = culturesToPublish.Select(c => c.Culture).ToArray(),
            });
        }

        var status = result.Status;
        var errorCategory = MapErrorCategory(status);

        // The failure message only carries the status; the offending aliases are the actionable part.
        var invalidAliases = result.Result?.InvalidPropertyAliases.ToArray() ?? [];
        if (invalidAliases.Length > 0)
        {
            context.LogWarning($"{item} has invalid property values: {string.Join(", ", invalidAliases)}");
        }

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to publish content '{contentKey}': {status}"),
            errorCategory);
    }

    /// <summary>
    /// From CMS 19, publishing named cultures of culture-variant content leaves shared (invariant)
    /// property changes unpublished unless one of the user's groups allows editing shared content.
    /// The CMS still reports success, so say so in the run log.
    /// </summary>
    private async Task WarnAboutUnpublishedSharedPropertiesAsync(
        ActionContext context,
        IPublishableContentBase? content,
        Guid userKey,
        List<CulturePublishScheduleModel> culturesToPublish,
        string item)
    {
        // Invariant content has no shared properties to hold back. The CMS rejects "*" for
        // culture-variant content, so named cultures are the only path that gets here with it.
        if (content is null || !content.ContentType.VariesByCulture() || culturesToPublish.Any(c => c.Culture is null or "*"))
        {
            return;
        }

        var user = await _userService.GetAsync(userKey);
        if (user is null || user.HasAccessToInvariantForVariant())
        {
            return;
        }

        var unpublished = content.Properties
            .Where(IsHeldBackWithoutSharedContentAccess)
            .Select(p => p.Alias)
            .ToArray();

        if (unpublished.Length == 0)
        {
            return;
        }

        context.LogWarning(
            $"{item} was published, but its shared properties ({string.Join(", ", unpublished)}) were not: " +
            "the service account's user groups don't allow editing shared content. " +
            "Enable \"Allow editing shared content\" on one of its groups to publish them.");
    }

    // The CMS skips a property that doesn't vary by culture on a culture publish when the user has no
    // shared content access (ContentRepositoryExtensions.PublishCulture). For most editors that leaves
    // the edited and published values apart. Block editors publish per culture instead
    // (CanMergePartialPropertyValues): a named-culture publish merges the structure and that culture's
    // values only, so pending blocks for another culture are expected. For those, run the merge the
    // missing permission would have run, for the invariant culture, and see if it would change anything.
    private bool IsHeldBackWithoutSharedContentAccess(IProperty property)
    {
        if (property.PropertyType.VariesByCulture())
        {
            return false;
        }

        var mergingEditor = GetMergingEditor(property.PropertyType);
        return property.Values
            .Where(v => v.Culture is null)
            .Any(v => mergingEditor is null
                ? !Equals(v.EditedValue, v.PublishedValue)
                : !SameValue(mergingEditor.MergePartialPropertyValueForCulture(v.EditedValue, v.PublishedValue, null), v.PublishedValue));
    }

    private IDataEditor? GetMergingEditor(IPropertyType propertyType)
        => _propertyEditors.TryGet(propertyType.PropertyEditorAlias, out var editor)
           && editor.CanMergePartialPropertyValues(propertyType)
            ? editor
            : null;

    // Block values are JSON strings, and a merge can serialize the same content with different
    // formatting, so compare them as JSON.
    private static bool SameValue(object? left, object? right)
    {
        if (Equals(left, right))
        {
            return true;
        }

        if (left is not string leftText || right is not string rightText)
        {
            return false;
        }

        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(leftText), JsonNode.Parse(rightText));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static List<CulturePublishScheduleModel> ParseCultures(string? cultures)
    {
        if (string.IsNullOrWhiteSpace(cultures))
        {
            // Invariant content — publish with null culture, no schedule.
            return [new CulturePublishScheduleModel { Culture = null, Schedule = null }];
        }

        return cultures
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(culture => new CulturePublishScheduleModel { Culture = culture, Schedule = null })
            .ToList();
    }

    private static StepRunErrorCategory MapErrorCategory(ContentPublishingOperationStatus status) => status switch
    {
        ContentPublishingOperationStatus.ContentNotFound => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.ContentInvalid => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.InvalidCulture => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.CultureMissing => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.MandatoryCultureMissing => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.PathNotPublished => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.InTrash => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.NothingToPublish => StepRunErrorCategory.Validation,
        ContentPublishingOperationStatus.CancelledByEvent => StepRunErrorCategory.Cancelled,
        ContentPublishingOperationStatus.ConcurrencyViolation => StepRunErrorCategory.ServiceUnavailable,
        _ => StepRunErrorCategory.Unknown,
    };
}
