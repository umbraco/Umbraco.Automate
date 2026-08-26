using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that fetches a media item from Umbraco by key and exposes its
/// properties to downstream steps.
/// </summary>
[Action("umbracoAutomate.getMedia", "Get Media",
    Description = "Fetches a media item and exposes its properties for use in later steps.",
    Group = "Media",
    Icon = "icon-picture",
    RequiredSections = [UmbracoConstants.Applications.Media])]
public sealed class GetMediaAction : ActionBase<GetMediaSettings, GetMediaOutput>
{
    // Not ICmsAction — this is a read, so no audit trail entry is written.

    /// <summary>
    /// Outcome emitted when the item is not present in the published media cache. Covers
    /// the "doesn't exist" and "is in the recycle bin" cases, which the cache reports
    /// uniformly.
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    private readonly IPublishedMediaCache _publishedMediaCache;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IPublishedUrlProvider _urlProvider;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly IContentValueNormaliser _normaliser;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly IVariationContextAccessor _variationContextAccessor;
    private readonly ILogger<GetMediaAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetMediaAction"/> class.
    /// </summary>
    public GetMediaAction(
        ActionInfrastructure infrastructure,
        IPublishedMediaCache publishedMediaCache,
        IUmbracoContextFactory umbracoContextFactory,
        IPublishedUrlProvider urlProvider,
        IUserIdKeyResolver userIdKeyResolver,
        IContentValueNormaliser normaliser,
        IAutomationActionAuthorizer authorizer,
        IVariationContextAccessor variationContextAccessor,
        ILogger<GetMediaAction> logger)
        : base(infrastructure)
    {
        _publishedMediaCache = publishedMediaCache;
        _umbracoContextFactory = umbracoContextFactory;
        _urlProvider = urlProvider;
        _userIdKeyResolver = userIdKeyResolver;
        _normaliser = normaliser;
        _authorizer = authorizer;
        _variationContextAccessor = variationContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<GetMediaSettings>();

        if (string.IsNullOrWhiteSpace(settings.MediaKey) ||
            !Guid.TryParse(settings.MediaKey, out var mediaKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing media key: '{settings.MediaKey}'."),
                StepRunErrorCategory.Validation);
        }

        // Node-level authorisation: section access is checked upstream by the middleware,
        // but the service account's start node may scope it to a subset of the Media
        // section. Reject reads outside the account's accessible path. Media access has
        // no per-permission verbs, so this is a binary allow/deny.
        if (await _authorizer.AuthorizeMediaOrFailAsync(mediaKey, cancellationToken) is { } failure)
        {
            return failure;
        }

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. No-op when a context is already in scope.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var media = await _publishedMediaCache.GetByIdAsync(mediaKey);
        if (media is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Media {MediaKey} not found in published cache.",
                context.AutomationId, context.RunId, mediaKey);

            return SuccessWithOutcome(OutcomeNotFound, new GetMediaOutput { MediaKey = mediaKey });
        }

        var culture = NormaliseCulture(settings.Culture, media);
        var creatorKey = await TryResolveUserKeyAsync(media.CreatorId);
        var writerKey = await TryResolveUserKeyAsync(media.WriterId);

        // Property reads need an ambient VariationContext to resolve the culture and segment
        // they were not given. There is none on the automation thread, and a null segment is
        // rejected by the published cache. See EnterVariationContext.
        using var variationScope = _variationContextAccessor.EnterVariationContext(culture);

        return Success(Project(media, culture, creatorKey, writerKey));
    }

    // IPublishedContent exposes user references as ints. We resolve them to Guid keys
    // because keys are the stable cross-system identifier we expose elsewhere in
    // automation outputs. TryGetAsync returns a failed Attempt if the user has been
    // deleted — surface that as null rather than failing the action.
    private async Task<Guid?> TryResolveUserKeyAsync(int userId)
    {
        var attempt = await _userIdKeyResolver.TryGetAsync(userId);
        return attempt.Success ? attempt.Result : null;
    }

    private static string? NormaliseCulture(string? requested, IPublishedContent media)
    {
        if (!media.ContentType.VariesByCulture())
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(requested)
            ? requested
            : media.Cultures.Keys.FirstOrDefault();
    }

    private GetMediaOutput Project(IPublishedContent media, string? culture, Guid? creatorKey, Guid? writerKey)
        => new()
        {
            MediaKey = media.Key,
            Name = ResolveName(media, culture),
            MediaTypeAlias = media.ContentType.Alias,
            MediaTypeKey = media.ContentType.Key,
            ParentKey = media.Parent?.Key,
            Level = media.Level,
            Path = media.Path,
            SortOrder = media.SortOrder,
            Url = ResolveUrl(media, culture),
            Culture = culture,
            AvailableCultures = media.Cultures.Keys.ToArray(),
            CreateDate = media.CreateDate,
            UpdateDate = media.UpdateDate,
            CreatorKey = creatorKey,
            WriterKey = writerKey,
            Properties = _normaliser.NormaliseProperties(media, culture),
        };

    // Media items have no Url() extension the way content does — the file URL is read
    // off the conventional 'umbracoFile' property. MediaUrl() returns string.Empty when
    // that property doesn't exist on the media type, which we normalise to null.
    private string? ResolveUrl(IPublishedContent media, string? culture)
    {
        var url = media.MediaUrl(_urlProvider, culture);
        return string.IsNullOrEmpty(url) ? null : url;
    }

    private static string? ResolveName(IPublishedContent media, string? culture)
    {
        if (culture is not null && media.Cultures.TryGetValue(culture, out var info))
        {
            return info.Name;
        }

        return media.Name;
    }
}
