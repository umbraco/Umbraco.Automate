using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Entities;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Search;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that searches media by name, optionally restricted by media type.
/// Searches the Umbraco Search media index — media has no draft/published split, so
/// unlike <see cref="FindContentAction"/> there is no "include unpublished" toggle or
/// second index to search.
/// </summary>
[Action("umbracoAutomate.findMedia", "Find Media",
    Description = "Finds media items by name, optionally filtered by media type.",
    Group = "Media",
    Icon = "icon-search",
    RequiredSections = [UmbracoConstants.Applications.Media])]
public sealed class FindMediaAction : ActionBase<FindMediaSettings, FindMediaOutput>
{
    /// <summary>
    /// Outcome key for the normal result. It is the default outcome, so it is also the key of
    /// the canvas handle that a connection from a successful step leaves from.
    /// </summary>
    public const string OutcomeSuccess = "success";

    // Not ICmsAction — this is a read, so no audit trail entry is written.

    /// <summary>Outcome emitted when no media matches.</summary>
    public const string OutcomeNotFound = "notFound";

    private const int MinLimit = 1;
    private const int MaxLimit = 500;

    private readonly ISearcherResolver _searcherResolver;
    private readonly IEntityService _entityService;
    private readonly IPublishedMediaCache _publishedMediaCache;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IPublishedUrlProvider _urlProvider;
    private readonly IAutomationActionAuthorizer _authorizer;
    private readonly ILogger<FindMediaAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FindMediaAction"/> class.
    /// </summary>
    public FindMediaAction(
        ActionInfrastructure infrastructure,
        ISearcherResolver searcherResolver,
        IEntityService entityService,
        IPublishedMediaCache publishedMediaCache,
        IUmbracoContextFactory umbracoContextFactory,
        IPublishedUrlProvider urlProvider,
        IAutomationActionAuthorizer authorizer,
        ILogger<FindMediaAction> logger)
        : base(infrastructure)
    {
        _searcherResolver = searcherResolver;
        _entityService = entityService;
        _publishedMediaCache = publishedMediaCache;
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
        var settings = context.GetSettings<FindMediaSettings>();

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
                    nameof(FindMediaSettings.Limit),
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

        // The picker stores media type keys, which are what the index holds, so they filter
        // directly. A stale key matches nothing; it doesn't fail the run.
        var mediaTypeKeys = FindByNameSearch.ParseKeys(settings.MediaTypes);

        const string indexAlias = UmbracoConstants.Search.IndexAliases.DraftMedia;
        var searcher = _searcherResolver.GetSearcher(indexAlias);
        if (searcher is null)
        {
            // Umbraco Search registers a searcher per index, so this indicates a
            // misconfigured host — treat as an infra/unknown failure, not validation.
            return ActionResult.Failed(
                new InvalidOperationException($"No searcher is registered for the Umbraco Search index '{indexAlias}'."),
                StepRunErrorCategory.Unknown);
        }

        // EnsureUmbracoContext is required when running from the outbox dispatcher, which
        // has no HTTP request scope. No-op when a context is already in scope. We need it
        // for the published-cache lookup that resolves URLs for each match.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        FindByNameSearchResult<FindMediaMatch> found;
        try
        {
            found = await FindByNameSearch.SearchAsync(
                searcher,
                indexAlias,
                settings.Name,
                matchMode,
                culture: null,
                FindByNameSearch.BuildFilters(settings.Name, mediaTypeKeys, UmbracoConstants.System.RecycleBinMediaKey),
                settings.Limit,
                LoadMatches,
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

        // Post-hoc per-node filter: drop results outside the service account's path.
        // LimitReached is computed before this filter so callers can still tell whether the
        // search hit the limit (and would have produced more matches the account simply
        // could not see).
        var preFilterCount = matches.Count;
        if (matches.Count > 0)
        {
            var authorizedKeys = await _authorizer.FilterAuthorizedMediaAsync(
                matches.Select(m => m.MediaKey),
                cancellationToken);

            matches = matches.Where(m => authorizedKeys.Contains(m.MediaKey)).ToList();
        }

        const string searched = "media";
        var hidden = preFilterCount - matches.Count;
        if (hidden > 0)
        {
            context.LogInfo($"{hidden} of {preFilterCount} matches were left out because the service account cannot access them");
        }

        if (matches.Count == 0)
        {
            context.LogInfo($"No {searched} found with a name matching '{settings.Name}' ({matchMode})");
            return SuccessWithOutcome(OutcomeNotFound, new FindMediaOutput());
        }

        var limitNote = found.LimitReached
            ? $"; the limit of {settings.Limit} was reached, so there may be more"
            : found.HitCeilingReached
                ? $"; the search stopped after examining {FindByNameSearch.MaxHitsExamined} results, so there may be more"
                : string.Empty;
        context.LogInfo($"Found {matches.Count} {searched} {(matches.Count == 1 ? "item" : "items")} with a name matching '{settings.Name}' ({matchMode}){limitNote}");

        return Success(new FindMediaOutput
        {
            Matches = matches,
            LimitReached = found.MoreMayExist,
        });
    }

    private IReadOnlyList<FindMediaMatch> LoadMatches(IReadOnlyList<Guid> keys)
    {
        var entities = _entityService
            .GetAll(UmbracoObjectTypes.Media, keys.ToArray())
            .OfType<IMediaEntitySlim>()
            .ToDictionary(entity => entity.Key);

        var matches = new List<FindMediaMatch>(keys.Count);
        foreach (var key in keys)
        {
            if (!entities.TryGetValue(key, out var entity))
            {
                continue;
            }

            matches.Add(new FindMediaMatch
            {
                MediaKey = key,
                Name = entity.Name,
                MediaTypeAlias = entity.ContentTypeAlias,
                Level = entity.Level,
                Path = entity.Path,
                Url = TryResolveUrl(key),
                CreateDate = entity.CreateDate,
                UpdateDate = entity.UpdateDate,
            });
        }

        return matches;
    }

    // Media items have no Url() extension the way content does — the file URL is read
    // off the conventional 'umbracoFile' property. MediaUrl() returns string.Empty when
    // that property doesn't exist on the media type, which we normalise to null.
    private string? TryResolveUrl(Guid key)
    {
        var media = _publishedMediaCache.GetById(key);
        if (media is null)
        {
            return null;
        }

        var url = media.MediaUrl(_urlProvider);
        return string.IsNullOrEmpty(url) ? null : url;
    }
}
