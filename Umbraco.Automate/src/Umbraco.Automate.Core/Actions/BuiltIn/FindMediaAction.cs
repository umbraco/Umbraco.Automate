using System.Globalization;
using Examine;
using Examine.Lucene.Providers;
using Examine.Lucene.Search;
using Examine.Search;
using Lucene.Net.QueryParsers.Classic;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that searches media by name, optionally restricted by media type.
/// Queries the Examine <c>ExternalIndex</c> — media has no draft/published split, so
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
    // Not ICmsAction — this is a read, so no audit trail entry is written.

    /// <summary>Outcome emitted when no media matches.</summary>
    public const string OutcomeNotFound = "notFound";

    private const int MinLimit = 1;
    private const int MaxLimit = 500;

    private readonly IExamineManager _examineManager;
    private readonly IMediaTypeService _mediaTypeService;
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
        IExamineManager examineManager,
        IMediaTypeService mediaTypeService,
        IPublishedMediaCache publishedMediaCache,
        IUmbracoContextFactory umbracoContextFactory,
        IPublishedUrlProvider urlProvider,
        IAutomationActionAuthorizer authorizer,
        ILogger<FindMediaAction> logger)
        : base(infrastructure)
    {
        _examineManager = examineManager;
        _mediaTypeService = mediaTypeService;
        _publishedMediaCache = publishedMediaCache;
        _umbracoContextFactory = umbracoContextFactory;
        _urlProvider = urlProvider;
        _authorizer = authorizer;
        _logger = logger;
    }

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

        // Picker stores media-type KEYS; Examine indexes only aliases, so resolve here.
        // A key that doesn't resolve is silently dropped — a stale picker selection
        // shouldn't hard-fail the automation. Note this means a CSV that resolves to zero
        // aliases (e.g. every key is stale) omits the type filter entirely rather than
        // matching nothing — BuildQuery only appends the filter clause when non-empty.
        var mediaTypeAliases = ResolveMediaTypeAliases(settings.MediaTypes);

        if (!_examineManager.TryGetIndex(UmbracoConstants.UmbracoIndexes.ExternalIndexName, out var index))
        {
            // Examine should be registered by default, so this indicates a
            // misconfigured host — treat as an infra/unknown failure, not validation.
            return ActionResult.Failed(
                new InvalidOperationException($"Examine index '{UmbracoConstants.UmbracoIndexes.ExternalIndexName}' is not available."),
                StepRunErrorCategory.Unknown);
        }

        var query = BuildQuery(settings.Name, matchMode, mediaTypeAliases);

        ISearchResults results;
        try
        {
            results = CreateNativeQuery(index.Searcher, query)
                .Execute(QueryOptions.SkipTake(0, settings.Limit));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Automation {AutomationId} / Run {RunId}: Examine query failed. Query: {Query}",
                context.AutomationId, context.RunId, query);

            return ActionResult.Failed(ex, StepRunErrorCategory.Unknown);
        }

        // EnsureUmbracoContext is required when running from the outbox dispatcher, which
        // has no HTTP request scope. No-op when a context is already in scope. We need it
        // for the published-cache lookup that resolves URLs for each match.
        using var contextRef = _umbracoContextFactory.EnsureUmbracoContext();

        var matches = results
            .Select(Project)
            .ToList();

        // Post-hoc per-node filter: drop results outside the service account's path.
        // LimitReached is computed against the pre-filter result count so callers can still tell
        // whether Examine hit the limit (and would have produced more matches the account simply
        // could not see).
        var preFilterCount = matches.Count;
        if (matches.Count > 0)
        {
            var authorizedKeys = await _authorizer.FilterAuthorizedMediaAsync(
                matches.Select(m => m.MediaKey),
                cancellationToken);

            matches = matches.Where(m => authorizedKeys.Contains(m.MediaKey)).ToList();
        }

        if (matches.Count == 0)
        {
            return SuccessWithOutcome(OutcomeNotFound, new FindMediaOutput());
        }

        return Success(new FindMediaOutput
        {
            Matches = matches,
            LimitReached = preFilterCount >= settings.Limit,
        });
    }

    /// <summary>
    /// Creates the native Examine query, enabling leading wildcards so the "Contains" match
    /// mode (which emits <c>nodeName:*term*</c>) does not throw at runtime. See
    /// <see cref="FindContentAction"/>'s equivalent for the full rationale.
    /// </summary>
    private static IBooleanOperation CreateNativeQuery(ISearcher searcher, string query)
    {
        if (searcher is BaseLuceneSearcher luceneSearcher)
        {
            return luceneSearcher
                .CreateQuery(
                    category: null,
                    defaultOperation: BooleanOperation.And,
                    luceneAnalyzer: luceneSearcher.LuceneAnalyzer,
                    searchOptions: new LuceneSearchOptions { AllowLeadingWildcard = true })
                .NativeQuery(query);
        }

        return searcher.CreateQuery().NativeQuery(query);
    }

    /// <summary>
    /// Builds the native Examine query. Visible for tests in the same assembly.
    /// </summary>
    internal static string BuildQuery(
        string name,
        FindContentMatchMode mode,
        IReadOnlyList<string> mediaTypeAliases)
    {
        var nameTerm = BuildNameTerm(name.Trim(), mode);

        var query = $"+__IndexType:media +(nodeName:{nameTerm})";

        if (mediaTypeAliases.Count > 0)
        {
            // Bracketed OR over media type aliases: +(__NodeTypeAlias:a __NodeTypeAlias:b)
            var aliasClause = string.Join(
                " ",
                mediaTypeAliases.Select(a => $"{ExamineFieldNames.ItemTypeFieldName}:{QueryParserBase.Escape(a.ToLowerInvariant())}"));
            query += $" +({aliasClause})";
        }

        return query;
    }

    private IReadOnlyList<string> ResolveMediaTypeAliases(string? mediaTypesCsv)
    {
        if (string.IsNullOrWhiteSpace(mediaTypesCsv))
        {
            return [];
        }

        var keys = mediaTypesCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => Guid.TryParse(part, out var key) ? (Guid?)key : null)
            .Where(k => k.HasValue)
            .Select(k => k!.Value)
            .ToArray();

        if (keys.Length == 0)
        {
            return [];
        }

        var aliases = new List<string>(keys.Length);
        foreach (var key in keys)
        {
            var mediaType = _mediaTypeService.Get(key);
            if (mediaType is null)
            {
                _logger.LogDebug(
                    "FindMedia: media type key {MediaTypeKey} not found, dropping from filter.",
                    key);
                continue;
            }

            aliases.Add(mediaType.Alias);
        }

        return aliases;
    }

    private static string BuildNameTerm(string name, FindContentMatchMode mode)
    {
        var lower = name.ToLowerInvariant();
        var escaped = QueryParserBase.Escape(lower);

        return mode switch
        {
            // Phrase match for Exact — the nodeName field is tokenised but phrase queries
            // still honour position, so only names that tokenise to exactly these terms match.
            FindContentMatchMode.Exact => $"\"{escaped}\"",
            FindContentMatchMode.StartsWith => $"{escaped}*",
            FindContentMatchMode.Contains => $"*{escaped}*",
            _ => $"\"{escaped}\"",
        };
    }

    private FindMediaMatch Project(ISearchResult result)
    {
        var key = TryParseGuid(result, UmbracoExamineFieldNames.NodeKeyFieldName);
        var url = key.HasValue ? TryResolveUrl(key.Value) : null;

        return new FindMediaMatch
        {
            MediaKey = key ?? Guid.Empty,
            Name = TryGetString(result, "nodeName"),
            MediaTypeAlias = TryGetString(result, ExamineFieldNames.ItemTypeFieldName) ?? string.Empty,
            Level = TryParseInt(result, "level"),
            Path = TryGetString(result, "__Path"),
            Url = url,
            CreateDate = TryParseDate(result, "createDate"),
            UpdateDate = TryParseDate(result, "updateDate"),
        };
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

    private static string? TryGetString(ISearchResult result, string field)
        => result.Values.TryGetValue(field, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : null;

    private static Guid? TryParseGuid(ISearchResult result, string field)
        => TryGetString(result, field) is { } raw && Guid.TryParse(raw, out var guid)
            ? guid
            : null;

    private static int TryParseInt(ISearchResult result, string field)
        => TryGetString(result, field) is { } raw
           && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;

    private static DateTime TryParseDate(ISearchResult result, string field)
    {
        var raw = TryGetString(result, field);
        if (raw is null)
        {
            return default;
        }

        // Umbraco's Examine indexer encodes dates via Lucene's DateTools, which writes
        // them as yyyyMMddHHmmssfff (milliseconds resolution) — the default DateTime
        // parsers don't recognise that shape, so try the exact format first.
        if (DateTime.TryParseExact(raw, "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact))
        {
            return exact;
        }

        // Fallback for indexers or custom fields that emit ISO-8601.
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
            ? value
            : default;
    }
}
