using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that creates a new content item (draft) under a parent in Umbraco CMS.
/// The item is saved but not published — combine with <see cref="PublishContentAction"/> in a
/// subsequent step to publish it.
/// </summary>
[Action("umbracoAutomate.createContent", "Create Content",
    Description = "Creates a new content item under a parent in Umbraco CMS (draft save).",
    Group = "Content",
    Icon = "icon-add",
    RequiredSections = [UmbracoConstants.Applications.Content],
    RequiredPermissions = [ActionNew.ActionLetter])]
public sealed class CreateContentAction : ActionBase<CreateContentSettings, CreateContentOutput>, ICmsAction
{
    /// <summary>
    /// Outcome emitted when the parent content item does not exist.
    /// </summary>
    public const string OutcomeParentNotFound = "parentNotFound";

    /// <summary>
    /// Outcome emitted when the content type alias doesn't resolve to a real content type.
    /// </summary>
    public const string OutcomeContentTypeNotFound = "contentTypeNotFound";

    private readonly IContentService _contentService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<CreateContentAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateContentAction"/> class.
    /// </summary>
    public CreateContentAction(
        ActionInfrastructure infrastructure,
        IContentService contentService,
        IContentTypeService contentTypeService,
        IUserIdKeyResolver userIdKeyResolver,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        ILogger<CreateContentAction> logger)
        : base(infrastructure)
    {
        _contentService = contentService;
        _contentTypeService = contentTypeService;
        _userIdKeyResolver = userIdKeyResolver;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<CreateContentSettings>();

        if (!TryReadContentTypeKey(settings.ContentType, out var contentTypeKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing content type: '{settings.ContentType}'."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            return ActionResult.Failed(
                new ArgumentException("Name is required."),
                StepRunErrorCategory.Validation);
        }

        // An empty parent means the content root, which the picker has no way to express.
        var atRoot = string.IsNullOrWhiteSpace(settings.ParentKey);

        Guid parentKey = Guid.Empty;
        if (!atRoot && !Guid.TryParse(settings.ParentKey, out parentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid parent key: '{settings.ParentKey}'."),
                StepRunErrorCategory.Validation);
        }

        // The root is not a node, so it takes its own check: a service account confined to a
        // start node can reach content inside it but must not write to the root.
        var failure = atRoot
            ? await _authorizer.AuthorizeContentRootOrFailAsync(RequiredPermissions, cancellationToken)
            : await _authorizer.AuthorizeContentOrFailAsync(parentKey, RequiredPermissions, cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        if (!atRoot && _contentService.GetById(parentKey) is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Parent content {ParentKey} not found.",
                context.AutomationId, context.RunId, parentKey);

            return SuccessWithOutcome(OutcomeParentNotFound, new CreateContentOutput
            {
                Name = settings.Name,
                ContentTypeKey = contentTypeKey,
                ParentKey = parentKey,
            });
        }

        var contentType = _contentTypeService.Get(contentTypeKey);
        if (contentType is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Content type {ContentTypeKey} not found.",
                context.AutomationId, context.RunId, contentTypeKey);

            return SuccessWithOutcome(OutcomeContentTypeNotFound, new CreateContentOutput
            {
                Name = settings.Name,
                ContentTypeKey = contentTypeKey,
                ParentKey = parentKey,
            });
        }

        // Unlike media, a content type has to opt in to sitting at the root. Checking it here
        // turns what would be a save-time throw into an actionable message.
        if (atRoot && !contentType.AllowedAsRoot)
        {
            return ActionResult.Failed(
                new ArgumentException(
                    $"Content type '{contentType.Alias}' is not allowed at the content root. " +
                    "Pick a parent, or enable 'Allow as root' on the content type."),
                StepRunErrorCategory.Validation);
        }

        var variesByCulture = (contentType.Variations & ContentVariation.Culture) != 0;
        if (variesByCulture && string.IsNullOrWhiteSpace(settings.Culture))
        {
            return ActionResult.Failed(
                new ArgumentException($"Culture is required because content type '{contentType.Alias}' varies by culture."),
                StepRunErrorCategory.Validation);
        }

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        var userId = await _userIdKeyResolver.GetAsync(userKey);

        // The int overload takes the root sentinel; the Guid one has no way to express it.
        var content = atRoot
            ? _contentService.Create(settings.Name, UmbracoConstants.System.Root, contentType.Alias, userId)
            : _contentService.Create(settings.Name, parentKey, contentType.Alias, userId);

        if (variesByCulture)
        {
            content.SetCultureName(settings.Name, settings.Culture!);
        }

        ApplyProperties(content, settings.PropertiesJson);

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. The save raises notifications (e.g. webhook delivery) that resolve
        // content URLs via UrlProvider, which requires an UmbracoContext.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = _contentService.Save(content, userId);

        if (result.Success)
        {
            return Success(new CreateContentOutput
            {
                ContentKey = content.Key,
                Name = settings.Name,
                ContentTypeKey = contentTypeKey,
                ContentTypeAlias = contentType.Alias,
                ParentKey = parentKey,
            });
        }

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to save new content under '{parentKey}': {result.Result}"),
            MapErrorCategory(result.Result));
    }

    /// <summary>
    /// Reads the content type key out of the picker's stored value. The picker is capped at a
    /// single selection, but it stores its value in the same comma-separated form the
    /// multi-select type pickers use, so take the first key it holds.
    /// </summary>
    private static bool TryReadContentTypeKey(string? pickerValue, out Guid contentTypeKey)
    {
        if (!string.IsNullOrWhiteSpace(pickerValue))
        {
            foreach (var part in pickerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(part, out contentTypeKey))
                {
                    return true;
                }
            }
        }

        contentTypeKey = Guid.Empty;
        return false;
    }

    /// <summary>
    /// Applies optional invariant property values from a JSON object. Malformed JSON and
    /// unknown property aliases are silently skipped — this is optional convenience config,
    /// not a required part of creating the content item.
    /// </summary>
    private static void ApplyProperties(IContent content, string? propertiesJson)
    {
        if (string.IsNullOrWhiteSpace(propertiesJson))
        {
            return;
        }

        Dictionary<string, string>? properties;
        try
        {
            properties = JsonSerializer.Deserialize<Dictionary<string, string>>(propertiesJson);
        }
        catch (JsonException)
        {
            return;
        }

        if (properties is null)
        {
            return;
        }

        foreach (var (alias, value) in properties)
        {
            if (content.Properties.Contains(alias))
            {
                content.SetValue(alias, value);
            }
        }
    }

    private static StepRunErrorCategory MapErrorCategory(OperationResultType status) => status switch
    {
        OperationResultType.FailedCancelledByEvent => StepRunErrorCategory.Cancelled,
        OperationResultType.FailedCannot => StepRunErrorCategory.Validation,
        OperationResultType.FailedExceptionThrown => StepRunErrorCategory.Unknown,
        _ => StepRunErrorCategory.Unknown,
    };
}
