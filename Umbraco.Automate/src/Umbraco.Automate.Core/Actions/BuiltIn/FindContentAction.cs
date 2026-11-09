using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Actions;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that searches content by name, optionally restricted by content type.
/// Searches the Umbraco Search published content index by default, or the draft content index
/// when <see cref="FindContentSettings.IncludeUnpublished"/> is true.
/// Items in the recycle bin are never matched.
/// </summary>
[Action("umbracoAutomate.findContent", "Find Content",
    Description = "Finds content items by name, optionally filtered by content type.",
    Group = "Content",
    Icon = "icon-search",
    RequiredSections = [UmbracoConstants.Applications.Content],
    RequiredPermissions = [ActionBrowse.ActionLetter])]
public sealed class FindContentAction : ActionBase<FindContentSettings, FindContentOutput>
{
    // Not ICmsAction — this is a read, so no audit trail entry is written.

    /// <summary>
    /// Outcome key for the normal result. It is the default outcome, so it is also the key of
    /// the canvas handle that a connection from a successful step leaves from.
    /// </summary>
    public const string OutcomeSuccess = "success";

    /// <summary>Outcome emitted when no content matches.</summary>
    public const string OutcomeNotFound = "notFound";

    private const int MinLimit = 1;
    private const int MaxLimit = 500;

    private readonly ISearcherResolver _searcherResolver;
    private readonly IEntityService _entityService;
    private readonly ILanguageService _languageService;
    private readonly IPublishedContentCache _publishedContentCache;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IPublishedUrlProvider _urlProvider;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<FindContentAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FindContentAction"/> class.
    /// </summary>
    public FindContentAction(
        ActionInfrastructure infrastructure,
        ISearcherResolver searcherResolver,
        IEntityService entityService,
        ILanguageService languageService,
        IPublishedContentCache publishedContentCache,
        IUmbracoContextFactory umbracoContextFactory,
        IPublishedUrlProvider urlProvider,
        IAutomationActionAuthorizer authorizer,
        ILogger<FindContentAction> logger)
        : base(infrastructure)
    {
        _searcherResolver = searcherResolver;
        _entityService = entityService;
        _languageService = languageService;
        _publishedContentCache = publishedContentCache;
        _umbracoContextFactory = umbracoContextFactory;
        _urlProvider = urlProvider;
        _authorizer = authorizer;
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
        var settings = context.GetSettings<FindContentSettings>();

        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            return ActionResult.Failed(
                new ArgumentException("Name is required."),
                StepRunErrorCategory.Validation);
        }

        if (FindByNameSearch.GetTerms(settings.Name).Length == 0)
        {
            return ActionResult.Failed(
                new ArgumentException("Name must contain at least one word that is not only wildcard characters (* or ?)."),
                StepRunErrorCategory.Validation);
        }

        if (settings.Limit < MinLimit || settings.Limit > MaxLimit)
        {
            return ActionResult.Failed(
                new ArgumentOutOfRangeException(
                    nameof(FindContentSettings.Limit),
                    settings.Limit,
                    $"Limit must be between {MinLimit} and {MaxLimit}."),
                StepRunErrorCategory.Validation);
        }

        if (!Enum.TryParse<FindContentMatchMode>(settings.MatchMode, ignoreCase: true, out var matchMode))
        {
            return ActionResult.Failed(
                new ArgumentException($"Unknown MatchMode '{settings.MatchMode}'. Expected one of: Exact, StartsWith, Contains."),
                StepRunErrorCategory.Validation);
        }

        // The picker stores content type keys, which are what the index holds, so they filter
        // directly. A stale key matches nothing; it doesn't fail the run.
        var contentTypeKeys = FindByNameSearch.ParseKeys(settings.ContentTypes);

        var indexAlias = settings.IncludeUnpublished
            ? UmbracoConstants.Search.IndexAliases.DraftContent
            : UmbracoConstants.Search.IndexAliases.PublishedContent;

        var searcher = _searcherResolver.GetSearcher(indexAlias);
        if (searcher is null)
        {
            // Umbraco Search registers a searcher per index, so this indicates a
            // misconfigured host — treat as an infra/unknown failure, not validation.
            return ActionResult.Failed(
                new InvalidOperationException($"No searcher is registered for the Umbraco Search index '{indexAlias}'."),
                StepRunErrorCategory.Unknown);
        }

        var requestedCulture = string.IsNullOrWhiteSpace(settings.Culture) ? null : settings.Culture.Trim();
        var searchCulture = await ResolveSearchCultureAsync(requestedCulture);

        // EnsureUmbracoContext is required when running from the outbox dispatcher, which
        // has no HTTP request scope. No-op when a context is already in scope. We need it
        // for the published-cache lookup that resolves URLs for each match.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        FindByNameSearchResult<FindContentMatch> found;
        try
        {
            found = await FindByNameSearch.SearchAsync(
                searcher,
                indexAlias,
                settings.Name,
                matchMode,
                searchCulture,
                FindByNameSearch.BuildFilters(settings.Name, contentTypeKeys, UmbracoConstants.System.RecycleBinContentKey),
                settings.Limit,
                keys => LoadMatches(keys, searchCulture, requestedCulture, settings.IncludeUnpublished),
                match => match.Name,
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "Automation {AutomationId} / Run {RunId}: Umbraco Search query failed on index {IndexAlias}.",
                context.AutomationId, context.RunId, indexAlias);

            return ActionResult.Failed(ex, StepRunErrorCategory.Unknown);
        }

        var matches = found.Matches.ToList();

        // Post-hoc per-node filter: drop results outside the service account's path / permissions.
        // LimitReached is computed before this filter so callers can still tell whether the
        // search hit the limit (and would have produced more matches the account simply
        // could not see).
        var preFilterCount = matches.Count;
        if (matches.Count > 0)
        {
            var authorizedKeys = await _authorizer.FilterAuthorizedContentAsync(
                matches.Select(m => m.ContentKey),
                RequiredPermissions.ToHashSet(),
                cancellationToken);

            matches = matches.Where(m => authorizedKeys.Contains(m.ContentKey)).ToList();
        }

        var searched = settings.IncludeUnpublished ? "published and unpublished content" : "published content";
        var hidden = preFilterCount - matches.Count;
        if (hidden > 0)
        {
            context.LogInfo($"{hidden} of {preFilterCount} matches were left out because the service account cannot access them");
        }

        if (matches.Count == 0)
        {
            context.LogInfo($"No {searched} found with a name matching '{settings.Name}' ({matchMode})");
            return SuccessWithOutcome(OutcomeNotFound, new FindContentOutput());
        }

        var limitNote = found.LimitReached
            ? $"; the limit of {settings.Limit} was reached, so there may be more"
            : found.HitCeilingReached
                ? $"; the search stopped after examining {FindByNameSearch.MaxHitsExamined} results, so there may be more"
                : string.Empty;
        context.LogInfo($"Found {matches.Count} {searched} {(matches.Count == 1 ? "item" : "items")} with a name matching '{settings.Name}' ({matchMode}){limitNote}");

        return Success(new FindContentOutput
        {
            Matches = matches,
            LimitReached = found.MoreMayExist,
        });
    }

    /// <summary>
    /// Works out which culture to search in. Umbraco Search returns invariant items plus those of
    /// one culture, and culture-variant items have no invariant entry, so with no culture set the
    /// default language is searched: its name is what a variant item's invariant name is.
    /// </summary>
    private async Task<string?> ResolveSearchCultureAsync(string? requestedCulture)
    {
        var languages = (await _languageService.GetAllAsync()).ToList();

        if (requestedCulture is null)
        {
            return languages.FirstOrDefault(language => language.IsDefault)?.IsoCode;
        }

        // The index compares culture codes exactly, so use the language's own spelling.
        return languages.FirstOrDefault(language =>
                string.Equals(language.IsoCode, requestedCulture, StringComparison.OrdinalIgnoreCase))?.IsoCode
            ?? requestedCulture;
    }

    private IReadOnlyList<FindContentMatch> LoadMatches(
        IReadOnlyList<Guid> keys,
        string? searchCulture,
        string? requestedCulture,
        bool includeUnpublished)
        => includeUnpublished
            ? LoadDraftMatches(keys, searchCulture, requestedCulture)
            : LoadPublishedMatches(keys, searchCulture, requestedCulture);

    // The published index matches published names, which can differ from the draft name, so
    // published matches are read from the published cache.
    private List<FindContentMatch> LoadPublishedMatches(
        IReadOnlyList<Guid> keys,
        string? searchCulture,
        string? requestedCulture)
    {
        var matches = new List<FindContentMatch>(keys.Count);
        foreach (var key in keys)
        {
            var content = _publishedContentCache.GetById(key);
            if (content is null)
            {
                continue;
            }

            var name = searchCulture is not null && content.Cultures.TryGetValue(searchCulture, out var cultureInfo)
                ? cultureInfo.Name
                : content.Name;

            matches.Add(new FindContentMatch
            {
                ContentKey = key,
                Name = name,
                ContentTypeAlias = content.ContentType.Alias,
                ContentTypeKey = content.ContentType.Key,
                Level = content.Level,
                Path = content.Path,
                Url = content.Url(_urlProvider, requestedCulture, UrlMode.Default),
                Culture = requestedCulture,
                CreateDate = content.CreateDate,
                UpdateDate = content.UpdateDate,
            });
        }

        return matches;
    }

    private List<FindContentMatch> LoadDraftMatches(
        IReadOnlyList<Guid> keys,
        string? searchCulture,
        string? requestedCulture)
    {
        var entities = _entityService
            .GetAll(UmbracoObjectTypes.Document, keys.ToArray())
            .OfType<IDocumentEntitySlim>()
            .ToDictionary(entity => entity.Key);

        var matches = new List<FindContentMatch>(keys.Count);
        foreach (var key in keys)
        {
            if (!entities.TryGetValue(key, out var entity))
            {
                continue;
            }

            var name = searchCulture is not null && entity.CultureNames.TryGetValue(searchCulture, out var cultureName)
                ? cultureName
                : entity.Name;

            // Only published items have URLs, so drafts that were never published resolve to null.
            var url = _publishedContentCache.GetById(key)?.Url(_urlProvider, requestedCulture, UrlMode.Default);

            matches.Add(new FindContentMatch
            {
                ContentKey = key,
                Name = name,
                ContentTypeAlias = entity.ContentTypeAlias,
                ContentTypeKey = entity.ContentTypeKey,
                Level = entity.Level,
                Path = entity.Path,
                Url = url,
                Culture = requestedCulture,
                CreateDate = entity.CreateDate,
                UpdateDate = entity.UpdateDate,
            });
        }

        return matches;
    }
}
