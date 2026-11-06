using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that fetches a <em>published</em> Library element from Umbraco by key
/// and exposes its properties to downstream steps.
/// </summary>
[Action("umbracoAutomate.getElement", "Get Element",
    Description = "Fetches a published Library element and exposes its properties for use in later steps.",
    Group = "Library",
    Icon = "icon-plugin",
    RequiredSections = [UmbracoConstants.Applications.Library],
    RequiredPermissions = [ActionElementBrowse.ActionLetter])]
public sealed class GetElementAction : ActionBase<GetElementSettings, GetElementOutput>
{
    // Not ICmsAction: this is a read, so no audit trail entry is written.

    /// <summary>
    /// Outcome emitted when the element is not present in the published cache. Covers the
    /// "doesn't exist", "is unpublished" and "is in the recycle bin" cases, which the published
    /// element cache reports uniformly.
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    /// <summary>
    /// Outcome key for a successful read. It is the default outcome, so it is also the key of
    /// the canvas handle that a connection from a found element leaves from.
    /// </summary>
    public const string OutcomeSuccess = "success";

    private readonly IPublishedElementCache _publishedElementCache;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly IContentValueNormaliser _normaliser;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly IVariationContextAccessor _variationContextAccessor;
    private readonly ILogger<GetElementAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetElementAction"/> class.
    /// </summary>
    public GetElementAction(
        ActionInfrastructure infrastructure,
        IPublishedElementCache publishedElementCache,
        IUmbracoContextFactory umbracoContextFactory,
        IUserIdKeyResolver userIdKeyResolver,
        IContentValueNormaliser normaliser,
        IAutomationActionAuthorizer authorizer,
        IVariationContextAccessor variationContextAccessor,
        ILogger<GetElementAction> logger)
        : base(infrastructure)
    {
        _publishedElementCache = publishedElementCache;
        _umbracoContextFactory = umbracoContextFactory;
        _userIdKeyResolver = userIdKeyResolver;
        _normaliser = normaliser;
        _authorizer = authorizer;
        _variationContextAccessor = variationContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public override IReadOnlyList<StepOutcome> GetOutcomes()
        =>
        [
            new StepOutcome(OutcomeSuccess, "#uaOutcomes_found") { IsDefault = true },
            new StepOutcome(OutcomeNotFound, "#uaOutcomes_notFound"),
        ];

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<GetElementSettings>();

        if (string.IsNullOrWhiteSpace(settings.ElementKey) ||
            !Guid.TryParse(settings.ElementKey, out var elementKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing element key: '{settings.ElementKey}'."),
                StepRunErrorCategory.Validation);
        }

        // Node-level authorisation. A key the CMS reports as not existing is not a permission
        // problem, so it routes to the notFound outcome, as an unpublished element does.
        var authorization = await _authorizer.AuthorizeElementAsync(elementKey, RequiredPermissions, cancellationToken);
        if (authorization.IsNotFound)
        {
            context.LogWarning($"Library element {elementKey} was not found");

            return SuccessWithOutcome(OutcomeNotFound, new GetElementOutput { ElementKey = elementKey });
        }

        if (!authorization.Authorized)
        {
            return authorization.ToFailedActionResult();
        }

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. No-op when a context is already in scope.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        // IPublishedElement has no Trashed flag, but the cache drops trashed elements, so an
        // element in the recycle bin comes back null here and takes notFound.
        var element = await _publishedElementCache.GetByIdAsync(elementKey);
        if (element is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Element {ElementKey} not found in published cache.",
                context.AutomationId, context.RunId, elementKey);

            context.LogWarning($"Library element {elementKey} was not found in the published cache");

            return SuccessWithOutcome(OutcomeNotFound, new GetElementOutput { ElementKey = elementKey });
        }

        var culture = NormaliseCulture(settings.Culture, element);
        var creatorKey = await TryResolveUserKeyAsync(element.CreatorId);
        var writerKey = await TryResolveUserKeyAsync(element.WriterId);

        // Property reads need an ambient VariationContext to resolve the culture and segment
        // they were not given. There is none on the automation thread. See EnterVariationContext.
        using var variationScope = _variationContextAccessor.EnterVariationContext(culture);

        context.LogInfo($"Read Library element {ActionLogFormat.Item(ResolveName(element, culture), elementKey)}");

        return Success(Project(element, culture, creatorKey, writerKey));
    }

    // IPublishedElement exposes user references as ints. Resolve them to Guid keys, the stable
    // cross-system identifier used in automation outputs. A deleted user surfaces as null.
    private async Task<Guid?> TryResolveUserKeyAsync(int userId)
    {
        var attempt = await _userIdKeyResolver.TryGetAsync(userId);
        return attempt.Success ? attempt.Result : null;
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

    private GetElementOutput Project(IPublishedElement element, string? culture, Guid? creatorKey, Guid? writerKey)
        => new()
        {
            ElementKey = element.Key,
            Name = ResolveName(element, culture),
            ContentTypeAlias = element.ContentType.Alias,
            ContentTypeKey = element.ContentType.Key,
            Culture = culture,
            AvailableCultures = element.Cultures.Keys.ToArray(),
            CreateDate = element.CreateDate,
            UpdateDate = element.UpdateDate,
            CreatorKey = creatorKey,
            WriterKey = writerKey,
            Properties = _normaliser.NormaliseProperties(element, culture),
        };

    private static string? ResolveName(IPublishedElement element, string? culture)
    {
        if (culture is not null && element.Cultures.TryGetValue(culture, out var info))
        {
            return info.Name;
        }

        return element.Name;
    }
}
