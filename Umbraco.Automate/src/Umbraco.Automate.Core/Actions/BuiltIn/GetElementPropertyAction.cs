using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that fetches a single property value from a published Library element
/// and exposes it to downstream steps without materialising the whole element.
/// </summary>
[Action("umbracoAutomate.getElementProperty", "Get Element Property",
    Description = "Fetches a single property value from a published Library element.",
    Group = "Library",
    Icon = "icon-plugin",
    RequiredSections = [UmbracoConstants.Applications.Library],
    RequiredPermissions = [ActionElementBrowse.ActionLetter])]
public sealed class GetElementPropertyAction : ActionBase<GetElementPropertySettings, GetElementPropertyOutput>
{
    /// <summary>
    /// Outcome key for the normal result. It is the default outcome, so it is also the key of
    /// the canvas handle that a connection from a successful step leaves from.
    /// </summary>
    public const string OutcomeSuccess = "success";

    /// <summary>
    /// Outcome emitted when the element is not present in the published cache (missing,
    /// unpublished or in the recycle bin).
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    /// <summary>
    /// Outcome emitted when the property alias doesn't exist on the element type. Separate from
    /// a null/empty value: this is a "wrong alias" signal that users may want to branch on.
    /// </summary>
    public const string OutcomePropertyNotFound = "propertyNotFound";

    private readonly IPublishedElementCache _publishedElementCache;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IContentValueNormaliser _normaliser;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly IVariationContextAccessor _variationContextAccessor;
    private readonly ILogger<GetElementPropertyAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetElementPropertyAction"/> class.
    /// </summary>
    public GetElementPropertyAction(
        ActionInfrastructure infrastructure,
        IPublishedElementCache publishedElementCache,
        IUmbracoContextFactory umbracoContextFactory,
        IContentValueNormaliser normaliser,
        IAutomationActionAuthorizer authorizer,
        IVariationContextAccessor variationContextAccessor,
        ILogger<GetElementPropertyAction> logger)
        : base(infrastructure)
    {
        _publishedElementCache = publishedElementCache;
        _umbracoContextFactory = umbracoContextFactory;
        _normaliser = normaliser;
        _authorizer = authorizer;
        _variationContextAccessor = variationContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public override IReadOnlyList<StepOutcome> GetOutcomes()
        =>
        [
            new StepOutcome(OutcomeSuccess, "#uaOutcomes_found") { IsDefault = true, Description = "#uaOutcomes_propertyFoundDescription" },
            new StepOutcome(OutcomeNotFound, "#uaOutcomes_notFound"),
            new StepOutcome(OutcomePropertyNotFound, "#uaOutcomes_propertyNotFound"),
        ];

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<GetElementPropertySettings>();

        if (string.IsNullOrWhiteSpace(settings.ElementKey) ||
            !Guid.TryParse(settings.ElementKey, out var elementKey))
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

        // A key the CMS reports as not existing is not a permission problem, so it routes to
        // the notFound outcome, as an unpublished element does.
        var authorization = await _authorizer.AuthorizeElementAsync(elementKey, RequiredPermissions, cancellationToken);
        if (authorization.IsNotFound)
        {
            context.LogWarning($"Library element {elementKey} was not found");

            return SuccessWithOutcome(OutcomeNotFound, new GetElementPropertyOutput
            {
                ElementKey = elementKey,
                PropertyAlias = settings.PropertyAlias,
            });
        }

        if (!authorization.Authorized)
        {
            return authorization.ToFailedActionResult();
        }

        // Required when running from the outbox dispatcher, which has no HTTP request scope.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        // The cache drops trashed elements, so an element in the recycle bin comes back null.
        var element = await _publishedElementCache.GetByIdAsync(elementKey);
        if (element is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Element {ElementKey} not found in published cache.",
                context.AutomationId, context.RunId, elementKey);

            context.LogWarning($"Library element {elementKey} was not found in the published cache");

            return SuccessWithOutcome(OutcomeNotFound, new GetElementPropertyOutput
            {
                ElementKey = elementKey,
                PropertyAlias = settings.PropertyAlias,
            });
        }

        var culture = NormaliseCulture(settings.Culture, element);

        if (element.GetProperty(settings.PropertyAlias) is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Property {PropertyAlias} not found on {ContentTypeAlias}.",
                context.AutomationId, context.RunId, settings.PropertyAlias, element.ContentType.Alias);

            context.LogWarning($"Property '{settings.PropertyAlias}' does not exist on {element.ContentType.Alias}");

            return SuccessWithOutcome(OutcomePropertyNotFound, new GetElementPropertyOutput
            {
                ElementKey = elementKey,
                PropertyAlias = settings.PropertyAlias,
                Culture = culture,
            });
        }

        // Property reads need an ambient VariationContext to resolve the culture and segment
        // they were not given. There is none on the automation thread. See EnterVariationContext.
        using var variationScope = _variationContextAccessor.EnterVariationContext(culture);

        var value = _normaliser.ReadProperty(element, settings.PropertyAlias, culture);

        context.LogInfo($"Read property '{settings.PropertyAlias}' from Library element {ActionLogFormat.Item(ResolveName(element, culture), elementKey)}");

        return Success(new GetElementPropertyOutput
        {
            ElementKey = elementKey,
            PropertyAlias = settings.PropertyAlias,
            Culture = culture,
            Value = value,
            HasValue = value is not null,
        });
    }

    private static string? NormaliseCulture(string? requested, IPublishedElement element)
    {
        if (!element.ContentType.VariesByCulture())
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(requested)
            ? requested
            : element.Cultures.Keys.FirstOrDefault();
    }

    private static string? ResolveName(IPublishedElement element, string? culture)
        => culture is not null && element.Cultures.TryGetValue(culture, out var info)
            ? info.Name
            : element.Name;
}
