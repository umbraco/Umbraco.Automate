using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that publishes a Library element in Umbraco CMS.
/// </summary>
[Action("umbracoAutomate.publishElement", "Publish Element",
    Description = "Publishes a Library element in Umbraco CMS.",
    Group = "Library",
    Icon = "icon-globe",
    RequiredSections = [UmbracoConstants.Applications.Library],
    RequiredPermissions = [ActionElementPublish.ActionLetter])]
public sealed class PublishElementAction : ActionBase<PublishElementSettings, PublishElementOutput>, ICmsAction
{
    private readonly IElementPublishingService _elementPublishingService;
    private readonly IElementService _elementService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<PublishElementAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="PublishElementAction"/> class.
    /// </summary>
    public PublishElementAction(
        ActionInfrastructure infrastructure,
        IElementPublishingService elementPublishingService,
        IElementService elementService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        ILogger<PublishElementAction> logger)
        : base(infrastructure)
    {
        _elementPublishingService = elementPublishingService;
        _elementService = elementService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<PublishElementSettings>();

        if (string.IsNullOrWhiteSpace(settings.ElementKey) || !Guid.TryParse(settings.ElementKey, out var elementKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing element key: '{settings.ElementKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (await _authorizer.AuthorizeElementOrFailAsync(elementKey, RequiredPermissions, cancellationToken) is { } failure)
        {
            return failure;
        }

        // A service account with a root start node is authorised for the recycle bin too, so a
        // trashed element gets this far. Fail it, as a missing one does.
        var element = _elementService.GetById(elementKey);
        if (element is null || element.Trashed)
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Library element '{elementKey}' was not found or is in the recycle bin."),
                StepRunErrorCategory.Validation);
        }

        var culturesToPublish = ParseCultures(settings.Cultures);

        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Publishing Library element {ElementKey} for cultures [{Cultures}]",
            context.AutomationId, context.RunId, elementKey,
            culturesToPublish.Count == 0 ? "invariant" : string.Join(", ", culturesToPublish.Select(c => c.Culture ?? "*")));

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        // Required when running from the outbox dispatcher, which has no HTTP request scope.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = await _elementPublishingService.PublishAsync(elementKey, culturesToPublish, userKey);

        var item = ActionLogFormat.Item(result.Result?.Content?.Name ?? element.Name, elementKey);
        var cultures = ActionLogFormat.Cultures(culturesToPublish.Select(c => c.Culture));

        if (result.Success)
        {
            context.LogInfo(cultures.Length == 0 ? $"Published {item}" : $"Published {item} in {cultures}");

            return Success(new PublishElementOutput
            {
                ElementKey = elementKey,
                Cultures = culturesToPublish.Select(c => c.Culture).ToArray(),
            });
        }

        var status = result.Status;

        // The failure message only carries the status; the offending aliases are the actionable part.
        var invalidAliases = result.Result?.InvalidPropertyAliases.ToArray() ?? [];
        if (invalidAliases.Length > 0)
        {
            context.LogWarning($"{item} has invalid property values: {string.Join(", ", invalidAliases)}");
        }

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to publish Library element '{elementKey}': {status}"),
            MapErrorCategory(status));
    }

    private static List<CulturePublishScheduleModel> ParseCultures(string? cultures)
    {
        if (string.IsNullOrWhiteSpace(cultures))
        {
            // Invariant element — publish with null culture, no schedule.
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
