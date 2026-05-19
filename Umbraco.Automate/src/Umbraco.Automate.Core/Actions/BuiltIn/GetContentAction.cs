using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Security;
using UmbracoConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that fetches a <em>published</em> content item from Umbraco by key
/// and exposes its properties to downstream steps.
/// </summary>
[Action("umbracoAutomate.getContent", "Get Content",
    Description = "Fetches a published content item and exposes its properties for use in later steps.",
    Group = "Content",
    Icon = "icon-document",
    RequiredSections = [UmbracoConstants.Applications.Content],
    RequiredPermissions = [ActionBrowse.ActionLetter])]
public sealed class GetContentAction : ActionBase<GetContentSettings, GetContentOutput>
{
    // Not ICmsAction — this is a read, so no audit trail entry is written.

    /// <summary>
    /// Outcome emitted when the item is not present in the published cache. Covers the
    /// "doesn't exist", "is unpublished", "is in trash", and "not published in the
    /// requested culture" cases, which the published cache reports uniformly.
    /// </summary>
    public const string OutcomeNotFound = "notFound";

    private readonly IPublishedContentCache _publishedContentCache;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IPublishedUrlProvider _urlProvider;
    private readonly IUserIdKeyResolver _userIdKeyResolver;
    private readonly IContentValueNormaliser _normaliser;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<GetContentAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetContentAction"/> class.
    /// </summary>
    public GetContentAction(
        ActionInfrastructure infrastructure,
        IPublishedContentCache publishedContentCache,
        IUmbracoContextFactory umbracoContextFactory,
        IPublishedUrlProvider urlProvider,
        IUserIdKeyResolver userIdKeyResolver,
        IContentValueNormaliser normaliser,
        IAutomationActionAuthorizer authorizer,
        ILogger<GetContentAction> logger)
        : base(infrastructure)
    {
        _publishedContentCache = publishedContentCache;
        _umbracoContextFactory = umbracoContextFactory;
        _urlProvider = urlProvider;
        _userIdKeyResolver = userIdKeyResolver;
        _normaliser = normaliser;
        _authorizer = authorizer;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<GetContentSettings>();

        if (string.IsNullOrWhiteSpace(settings.ContentKey) ||
            !Guid.TryParse(settings.ContentKey, out var contentKey))
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid or missing content key: '{settings.ContentKey}'."),
                StepRunErrorCategory.Validation);
        }

        // Node-level authorisation: section access is checked upstream by the middleware,
        // but the service account's start node / granular permissions may scope it to a
        // subset of the Content section. Reject reads outside the account's accessible path.
        var auth = await _authorizer.AuthorizeContentAsync(
            contentKey,
            RequiredPermissions.ToHashSet(),
            cancellationToken);
        if (!auth.Authorized)
        {
            return ActionResult.Failed(
                new UnauthorizedAccessException(auth.FailureReason),
                StepRunErrorCategory.Authentication);
        }

        // Required when running from the outbox dispatcher, which has no HTTP request
        // scope. No-op when a context is already in scope.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var content = await _publishedContentCache.GetByIdAsync(contentKey);
        if (content is null)
        {
            _logger.LogDebug(
                "Automation {AutomationId} / Run {RunId}: Content {ContentKey} not found in published cache.",
                context.AutomationId, context.RunId, contentKey);

            return SuccessWithOutcome(OutcomeNotFound, new GetContentOutput { ContentKey = contentKey });
        }

        var culture = NormaliseCulture(settings.Culture, content);
        var creatorKey = await TryResolveUserKeyAsync(content.CreatorId);
        var writerKey = await TryResolveUserKeyAsync(content.WriterId);

        return Success(Project(content, culture, creatorKey, writerKey));
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

    private static string? NormaliseCulture(string? requested, IPublishedContent content)
    {
        if (!content.ContentType.VariesByCulture())
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(requested)
            ? requested
            : content.Cultures.Keys.FirstOrDefault();
    }

    private GetContentOutput Project(IPublishedContent content, string? culture, Guid? creatorKey, Guid? writerKey)
        => new()
        {
            ContentKey = content.Key,
            Name = ResolveName(content, culture),
            ContentTypeAlias = content.ContentType.Alias,
            ContentTypeKey = content.ContentType.Key,
            ParentKey = content.Parent?.Key,
            Level = content.Level,
            Path = content.Path,
            SortOrder = content.SortOrder,
            Url = content.Url(_urlProvider, culture, UrlMode.Default),
            Culture = culture,
            AvailableCultures = content.Cultures.Keys.ToArray(),
            CreateDate = content.CreateDate,
            UpdateDate = content.UpdateDate,
            CreatorKey = creatorKey,
            WriterKey = writerKey,
            Properties = _normaliser.NormaliseProperties(content, culture),
        };

    private static string? ResolveName(IPublishedContent content, string? culture)
    {
        if (culture is not null && content.Cultures.TryGetValue(culture, out var info))
        {
            return info.Name;
        }

        return content.Name;
    }
}
