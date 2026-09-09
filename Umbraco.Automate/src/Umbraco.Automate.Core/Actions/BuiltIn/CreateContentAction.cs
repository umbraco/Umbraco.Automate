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

        if (string.IsNullOrWhiteSpace(settings.ContentTypeAlias))
        {
            return ActionResult.Failed(
                new ArgumentException("Content type alias is required."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            return ActionResult.Failed(
                new ArgumentException("Name is required."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.ParentKey) || !Guid.TryParse(settings.ParentKey, out var parentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing parent key: '{settings.ParentKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (await _authorizer.AuthorizeContentOrFailAsync(parentKey, RequiredPermissions, cancellationToken) is { } failure)
        {
            return failure;
        }

        var parent = _contentService.GetById(parentKey);
        if (parent is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Parent content {ParentKey} not found.",
                context.AutomationId, context.RunId, parentKey);

            return SuccessWithOutcome(OutcomeParentNotFound, new CreateContentOutput
            {
                Name = settings.Name,
                ContentTypeAlias = settings.ContentTypeAlias,
                ParentKey = parentKey,
            });
        }

        var contentType = _contentTypeService.Get(settings.ContentTypeAlias);
        if (contentType is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Content type {ContentTypeAlias} not found.",
                context.AutomationId, context.RunId, settings.ContentTypeAlias);

            return SuccessWithOutcome(OutcomeContentTypeNotFound, new CreateContentOutput
            {
                Name = settings.Name,
                ContentTypeAlias = settings.ContentTypeAlias,
                ParentKey = parentKey,
            });
        }

        var variesByCulture = (contentType.Variations & ContentVariation.Culture) != 0;
        if (variesByCulture && string.IsNullOrWhiteSpace(settings.Culture))
        {
            return ActionResult.Failed(
                new ArgumentException($"Culture is required because content type '{settings.ContentTypeAlias}' varies by culture."),
                StepRunErrorCategory.Validation);
        }

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        var userId = await _userIdKeyResolver.GetAsync(userKey);

        var content = _contentService.Create(settings.Name, parentKey, contentType.Alias, userId);

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
                ContentTypeAlias = contentType.Alias,
                ParentKey = parentKey,
            });
        }

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to save new content under '{parentKey}': {result.Result}"),
            MapErrorCategory(result.Result));
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
