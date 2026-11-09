using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that writes a single property value on a Library element.
/// The change is persisted via <see cref="IPublishableContentService{TContent}.Save"/> — combine with
/// a publish step to publish the result.
/// </summary>
[Action("umbracoAutomate.updateElementProperty", "Update Element Property",
    Description = "Writes a single property value on a Library element.",
    Group = "Library",
    Icon = "icon-edit",
    RequiredSections = [UmbracoConstants.Applications.Library],
    RequiredPermissions = [ActionElementUpdate.ActionLetter])]
public sealed class UpdateElementPropertyAction : ActionBase<UpdateElementPropertySettings, UpdateElementPropertyOutput>, ICmsAction
{
    /// <summary>
    /// Outcome key for the normal result. It is the default outcome, so it is also the key of
    /// the canvas handle that a connection from a successful step leaves from.
    /// </summary>
    public const string OutcomeSuccess = "success";

    /// <summary>
    /// Outcome emitted when the Library element does not exist or is in the recycle bin.
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    /// <summary>
    /// Outcome emitted when the property alias doesn't exist on the element type.
    /// </summary>
    public const string OutcomePropertyNotFound = "propertyNotFound";

    private readonly IElementService _elementService;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<UpdateElementPropertyAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateElementPropertyAction"/> class.
    /// </summary>
    public UpdateElementPropertyAction(
        ActionInfrastructure infrastructure,
        IElementService elementService,
        IUserIdKeyResolver userIdKeyResolver,
        IBackOfficeSecurityAccessor backOfficeSecurityAccessor,
        IUmbracoContextFactory umbracoContextFactory,
        IAutomationActionAuthorizer authorizer,
        ILogger<UpdateElementPropertyAction> logger)
        : base(infrastructure)
    {
        _elementService = elementService;
        _userIdKeyResolver = userIdKeyResolver;
        _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        _umbracoContextFactory = umbracoContextFactory;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override IReadOnlyList<StepOutcome> GetOutcomes()
        =>
        [
            new StepOutcome(OutcomeSuccess, "#uaOutcomes_updated") { IsDefault = true },
            new StepOutcome(OutcomeNotFound, "#uaOutcomes_notFound"),
            new StepOutcome(OutcomePropertyNotFound, "#uaOutcomes_propertyNotFound"),
        ];

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<UpdateElementPropertySettings>();

        if (string.IsNullOrWhiteSpace(settings.ElementKey) || !Guid.TryParse(settings.ElementKey, out var elementKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing element key: '{settings.ElementKey}'."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.PropertyAlias))
        {
            return ActionResult.Failed(
                new ArgumentException("Property alias is required."),
                StepRunErrorCategory.Validation);
        }

        // A key the CMS reports as not existing (e.g. deleted) is not a permission problem, so it
        // routes to the notFound outcome.
        var authorization = await _authorizer.AuthorizeElementAsync(elementKey, RequiredPermissions, cancellationToken);
        if (authorization.IsNotFound)
        {
            context.LogWarning($"Library element {elementKey} was not found, so nothing was updated");

            return SuccessWithOutcome(OutcomeNotFound, new UpdateElementPropertyOutput
            {
                ElementKey = elementKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = settings.Culture,
                Segment = settings.Segment,
            });
        }

        if (!authorization.Authorized)
        {
            return authorization.ToFailedActionResult();
        }

        // A service account with a root start node is authorised for the recycle bin too, so a
        // trashed item gets this far. Treat it as missing, as every other account already does.
        var element = _elementService.GetById(elementKey);
        if (element is null || element.Trashed)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Library element {ElementKey} not found.",
                context.AutomationId, context.RunId, elementKey);

            context.LogWarning($"Library element {elementKey} was not found, so nothing was updated");

            return SuccessWithOutcome(OutcomeNotFound, new UpdateElementPropertyOutput
            {
                ElementKey = elementKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = settings.Culture,
                Segment = settings.Segment,
            });
        }

        if (!element.Properties.Contains(settings.PropertyAlias))
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Property {PropertyAlias} not found on {ElementTypeAlias}.",
                context.AutomationId, context.RunId, settings.PropertyAlias, element.ContentType.Alias);

            context.LogWarning($"Property '{settings.PropertyAlias}' does not exist on {element.ContentType.Alias}, so nothing was updated");

            return SuccessWithOutcome(OutcomePropertyNotFound, new UpdateElementPropertyOutput
            {
                ElementKey = elementKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = settings.Culture,
                Segment = settings.Segment,
            });
        }

        var culture = NormaliseCulture(settings.Culture, element);
        var segment = string.IsNullOrWhiteSpace(settings.Segment) ? null : settings.Segment;

        var previousValue = element.GetValue(settings.PropertyAlias, culture, segment);

        element.SetValue(settings.PropertyAlias, settings.Value, culture, segment);

        var userKey = _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Key
            ?? context.ExecutionContext?.ServiceAccountKey
            ?? throw new InvalidOperationException("No backoffice identity available. Ensure the automation is running within a workspace with a valid service account.");

        var userId = await _userIdKeyResolver.GetAsync(userKey);

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. The save raises notifications (e.g. webhook delivery) that resolve
        // URLs via UrlProvider, which requires an UmbracoContext.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var result = _elementService.Save(element, userId);

        if (result.Success)
        {
            context.LogInfo(DescribeUpdate(settings.PropertyAlias, ActionLogFormat.Item(element.Name, elementKey), settings.Culture, culture, segment));

            return Success(new UpdateElementPropertyOutput
            {
                ElementKey = elementKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = culture,
                Segment = segment,
                PreviousValue = previousValue,
            });
        }

        return ActionResult.Failed(
            new InvalidOperationException($"Failed to save Library element '{elementKey}': {result.Result}"),
            MapErrorCategory(result.Result));
    }

    private static string DescribeUpdate(string alias, string item, string? requestedCulture, string? culture, string? segment)
    {
        var variant = segment is null ? culture : $"{culture ?? "invariant"} / {segment}";
        var description = variant is null
            ? $"Updated '{alias}' on {item}, saved as a draft"
            : $"Updated '{alias}' on {item} ({variant}), saved as a draft";

        // A culture-variant item with no culture requested falls back to its first culture, which
        // the Input tab cannot show.
        return culture is not null && string.IsNullOrWhiteSpace(requestedCulture)
            ? $"{description}; no culture was given, so {culture} was used"
            : description;
    }

    private static string? NormaliseCulture(string? requested, IElement element)
    {
        var variesByCulture = (element.ContentType.Variations & ContentVariation.Culture) != 0;
        if (!variesByCulture)
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(requested)
            ? requested
            : element.AvailableCultures.FirstOrDefault();
    }

    private static StepRunErrorCategory MapErrorCategory(OperationResultType status) => status switch
    {
        OperationResultType.FailedCancelledByEvent => StepRunErrorCategory.Cancelled,
        OperationResultType.FailedCannot => StepRunErrorCategory.Validation,
        OperationResultType.FailedExceptionThrown => StepRunErrorCategory.Unknown,
        _ => StepRunErrorCategory.Unknown,
    };
}
