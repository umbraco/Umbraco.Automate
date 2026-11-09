using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that creates a new Library element (draft) in a folder or at the Library
/// root. The element is saved but not published; combine with <see cref="PublishElementAction"/>
/// in a later step to publish it.
/// </summary>
[Action("umbracoAutomate.createElement", "Create Element",
    Description = "Creates a new Library element in Umbraco CMS (draft save).",
    Group = "Library",
    Icon = "icon-add",
    RequiredSections = [UmbracoConstants.Applications.Library],
    RequiredPermissions = [ActionElementNew.ActionLetter])]
public sealed class CreateElementAction : ActionBase<CreateElementSettings, CreateElementOutput>, ICmsAction
{
    /// <summary>
    /// Outcome key for the normal result. It is the default outcome, so it is also the key of
    /// the canvas handle that a connection from a successful step leaves from.
    /// </summary>
    public const string OutcomeSuccess = "success";

    /// <summary>
    /// Outcome emitted when the folder does not exist or is in the recycle bin.
    /// </summary>
    public const string OutcomeParentNotFound = "parentNotFound";

    /// <summary>
    /// Outcome emitted when the element type doesn't resolve to a real element type.
    /// </summary>
    public const string OutcomeElementTypeNotFound = "elementTypeNotFound";

    private readonly IElementEditingService _elementEditingService;
    private readonly IElementContainerService _elementContainerService;
    private readonly IElementService _elementService;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly IContentTypeService _contentTypeService;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly ILogger<CreateElementAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateElementAction"/> class.
    /// </summary>
    public CreateElementAction(
        ActionInfrastructure infrastructure,
        IElementEditingService elementEditingService,
        IElementContainerService elementContainerService,
        IElementService elementService,
        IUserIdKeyResolver userIdKeyResolver,
        IContentTypeService contentTypeService,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        ICoreScopeProvider scopeProvider,
        ILogger<CreateElementAction> logger)
        : base(infrastructure)
    {
        _elementEditingService = elementEditingService;
        _elementContainerService = elementContainerService;
        _elementService = elementService;
        _userIdKeyResolver = userIdKeyResolver;
        _contentTypeService = contentTypeService;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _scopeProvider = scopeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public override IReadOnlyList<StepOutcome> GetOutcomes()
        =>
        [
            new StepOutcome(OutcomeSuccess, "#uaOutcomes_created") { IsDefault = true },
            new StepOutcome(OutcomeParentNotFound, "#uaOutcomes_folderNotFound"),
            new StepOutcome(OutcomeElementTypeNotFound, "#uaOutcomes_elementTypeNotFound"),
        ];

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<CreateElementSettings>();

        if (!TryReadElementTypeKey(settings.ElementType, out var elementTypeKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing element type: '{settings.ElementType}'."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            return ActionResult.Failed(
                new ArgumentException("Name is required."),
                StepRunErrorCategory.Validation);
        }

        // An empty folder means the Library root, which the picker has no way to express.
        Guid? folderKey = null;
        if (!string.IsNullOrWhiteSpace(settings.FolderKey))
        {
            if (!Guid.TryParse(settings.FolderKey, out var parsedFolderKey))
            {
                return ActionResult.Failed(
                    new ArgumentException($"Invalid folder key: '{settings.FolderKey}'."),
                    StepRunErrorCategory.Validation);
            }

            folderKey = parsedFolderKey;
        }

        // A missing folder, or one in the recycle bin that an account confined to a start node
        // can't see, comes back as not found. That routes to parentNotFound rather than failing
        // as a permission problem.
        var authorization = await _authorizer.AuthorizeElementParentAsync(
            folderKey,
            RequiredPermissions,
            cancellationToken);
        if (authorization.IsNotFound)
        {
            return ParentNotFound(context, settings.Name, elementTypeKey, folderKey);
        }

        if (!authorization.Authorized)
        {
            return authorization.ToFailedActionResult();
        }

        // An account with a root start node is authorised for the recycle bin too, and
        // CreateAsync doesn't refuse a trashed folder, so check it here.
        string? folderName = null;
        if (folderKey is { } key)
        {
            var folder = await _elementContainerService.GetAsync(key);
            if (folder is null || folder.Trashed)
            {
                return ParentNotFound(context, settings.Name, elementTypeKey, folderKey);
            }

            folderName = folder.Name;
        }

        var contentType = _contentTypeService.Get(elementTypeKey);
        if (contentType is null)
        {
            return ElementTypeNotFound(context, settings.Name, elementTypeKey, folderKey);
        }

        if (!contentType.IsElement)
        {
            return ActionResult.Failed(
                new ArgumentException($"Content type '{contentType.Alias}' is not an element type."),
                StepRunErrorCategory.Validation);
        }

        var variesByCulture = (contentType.Variations & ContentVariation.Culture) != 0;
        if (variesByCulture && string.IsNullOrWhiteSpace(settings.Culture))
        {
            return ActionResult.Failed(
                new ArgumentException($"Culture is required because element type '{contentType.Alias}' varies by culture."),
                StepRunErrorCategory.Validation);
        }

        var culture = variesByCulture ? settings.Culture : null;

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        var createModel = new ElementCreateModel
        {
            ContentTypeKey = elementTypeKey,
            ParentKey = folderKey,
            Variants = [new VariantModel { Culture = culture, Name = settings.Name }],
        };

        // Required when running from the outbox dispatcher, which has no HTTP request scope.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        // The create and the follow-up save run in one scope: the CMS services' own scopes nest
        // into it, and nothing is committed (or its notifications published) unless both succeed.
        using var scope = _scopeProvider.CreateCoreScope();

        // The name goes in alone so the CMS still applies its Library and folder filters. Property
        // values are applied afterwards in the same JSON format as Create Content: passing them to
        // CreateAsync would run them through each editor's backoffice-format conversion.
        var result = await _elementEditingService.CreateAsync(createModel, userKey);

        if (!result.Success || result.Result.Content is null)
        {
            return MapFailure(context, result.Status, settings.Name, contentType.Alias, elementTypeKey, folderKey, folderName);
        }

        var element = result.Result.Content;

        // With no values sent, a PropertyValidationError status only means mandatory properties
        // aren't set yet; the element is saved as a draft either way.
        if (HasPropertyValues(settings.PropertiesJson))
        {
            var applied = ApplyProperties(context, element, contentType, settings.PropertiesJson!, culture);
            if (applied)
            {
                var userId = await _userIdKeyResolver.GetAsync(userKey);
                var saveResult = _elementService.Save(element, userId);
                if (!saveResult.Success)
                {
                    var failedLocation = folderName is null ? "at the Library root" : $"in folder '{folderName}'";
                    context.LogError(
                        $"Library element '{element.Name}' was not created {failedLocation} because saving its property values failed");

                    return ActionResult.Failed(
                        new InvalidOperationException(
                            $"Library element '{element.Name}' was not created because saving its property values failed: {saveResult.Result}"),
                        MapSaveErrorCategory(saveResult.Result));
                }
            }
        }

        scope.Complete();

        var location = folderName is null ? "at the Library root" : $"in folder '{folderName}'";
        var inCulture = culture is null ? string.Empty : $" in {culture}";
        context.LogInfo($"Created {ActionLogFormat.Item(element.Name, element.Key)} as {contentType.Alias} {location}{inCulture}");

        return Success(new CreateElementOutput
        {
            ElementKey = element.Key,
            Name = settings.Name,
            ContentTypeKey = elementTypeKey,
            ContentTypeAlias = contentType.Alias,
            FolderKey = folderKey,
        });
    }

    /// <summary>
    /// Reads the element type key out of the picker's stored value. The picker is capped at a
    /// single selection, but it stores its value comma-separated, so take the first key it holds.
    /// </summary>
    private static bool TryReadElementTypeKey(string? pickerValue, out Guid elementTypeKey)
    {
        if (!string.IsNullOrWhiteSpace(pickerValue))
        {
            foreach (var part in pickerValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Guid.TryParse(part, out elementTypeKey))
                {
                    return true;
                }
            }
        }

        elementTypeKey = Guid.Empty;
        return false;
    }

    private ActionResult MapFailure(
        ActionContext context,
        ContentEditingOperationStatus status,
        string name,
        string typeAlias,
        Guid elementTypeKey,
        Guid? folderKey,
        string? folderName)
    {
        switch (status)
        {
            case ContentEditingOperationStatus.ParentNotFound:
                return ParentNotFound(context, name, elementTypeKey, folderKey);

            case ContentEditingOperationStatus.ContentTypeNotFound:
                return ElementTypeNotFound(context, name, elementTypeKey, folderKey);

            // One status covers a type that isn't "Allow in Library" and a folder that refuses it.
            case ContentEditingOperationStatus.NotAllowed:
                var where = folderName is null ? "at the Library root" : $"in folder '{folderName}'";
                return ActionResult.Failed(
                    new ArgumentException(
                        $"Element type '{typeAlias}' is not allowed {where}. Enable 'Allow in Library' on the element type, " +
                        "or add it to the allowed types of the folder."),
                    StepRunErrorCategory.Validation);

            default:
                return ActionResult.Failed(
                    new InvalidOperationException($"Failed to create Library element '{name}': {status}"),
                    MapErrorCategory(status));
        }
    }

    private ActionResult ParentNotFound(ActionContext context, string name, Guid elementTypeKey, Guid? folderKey)
    {
        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Library folder {FolderKey} not found.",
            context.AutomationId, context.RunId, folderKey);

        context.LogWarning($"Library folder {folderKey} was not found, so nothing was created");

        return SuccessWithOutcome(OutcomeParentNotFound, new CreateElementOutput
        {
            Name = name,
            ContentTypeKey = elementTypeKey,
            FolderKey = folderKey,
        });
    }

    private ActionResult ElementTypeNotFound(ActionContext context, string name, Guid elementTypeKey, Guid? folderKey)
    {
        _logger.LogDebug(
            "Automation {AutomationId} / Run {RunId}: Element type {ElementTypeKey} not found.",
            context.AutomationId, context.RunId, elementTypeKey);

        context.LogWarning($"Element type {elementTypeKey} was not found, so nothing was created");

        return SuccessWithOutcome(OutcomeElementTypeNotFound, new CreateElementOutput
        {
            Name = name,
            ContentTypeKey = elementTypeKey,
            FolderKey = folderKey,
        });
    }

    private static bool HasPropertyValues(string? propertiesJson)
        => !string.IsNullOrWhiteSpace(propertiesJson);

    /// <summary>
    /// Applies property values from a JSON object, as Create Content does. Culture-variant
    /// properties are set for <paramref name="culture"/>; invariant properties ignore it.
    /// Malformed JSON and unknown aliases are skipped with a warning in the run log.
    /// </summary>
    /// <returns>True when at least one value was set on the element.</returns>
    private static bool ApplyProperties(ActionContext context, IElement element, IContentTypeComposition contentType, string propertiesJson, string? culture)
    {
        var properties = PropertyValuesJson.TryParse(propertiesJson);
        if (properties is null)
        {
            context.LogWarning("Properties are not a valid JSON object and were ignored");
            return false;
        }

        var applied = false;
        foreach (var (alias, value) in properties)
        {
            var propertyType = contentType.CompositionPropertyTypes.FirstOrDefault(p => string.Equals(p.Alias, alias, StringComparison.OrdinalIgnoreCase));
            if (propertyType is null)
            {
                context.LogWarning($"Property '{alias}' does not exist on {contentType.Alias} and was skipped");
                continue;
            }

            // A culture-variant property rejects a value with no culture, and an invariant one
            // rejects a value with one.
            element.SetValue(propertyType.Alias, value, propertyType.VariesByCulture() ? culture : null);
            applied = true;
        }

        return applied;
    }

    private static StepRunErrorCategory MapSaveErrorCategory(OperationResultType status) => status switch
    {
        OperationResultType.FailedCancelledByEvent => StepRunErrorCategory.Cancelled,
        OperationResultType.FailedCannot => StepRunErrorCategory.Validation,
        _ => StepRunErrorCategory.Unknown,
    };

    private static StepRunErrorCategory MapErrorCategory(ContentEditingOperationStatus status) => status switch
    {
        ContentEditingOperationStatus.CancelledByNotification => StepRunErrorCategory.Cancelled,
        ContentEditingOperationStatus.ContentTypeCultureVarianceMismatch => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.ParentInvalid => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.PropertyTypeNotFound => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.PropertyTypeCultureVarianceMismatch => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.PropertyTypeSegmentVarianceMismatch => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.InvalidCulture => StepRunErrorCategory.Validation,
        ContentEditingOperationStatus.DuplicateName => StepRunErrorCategory.Validation,
        _ => StepRunErrorCategory.Unknown,
    };
}
