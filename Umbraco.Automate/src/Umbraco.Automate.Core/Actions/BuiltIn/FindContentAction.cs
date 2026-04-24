using System.Globalization;
using Examine;
using Examine.Search;
using Lucene.Net.QueryParsers.Classic;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Examine;
using Umbraco.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that searches content by name, optionally restricted by content type.
/// Queries the Examine <c>ExternalIndex</c> (published content) by default, or the
/// <c>InternalIndex</c> when <see cref="FindContentSettings.IncludeUnpublished"/> is true.
/// </summary>
[Action("umbracoAutomate.findContent", "Find Content",
    Description = "Finds content items by name, optionally filtered by content type.",
    Group = "Content",
    Icon = "icon-search")]
public sealed class FindContentAction : ActionBase<FindContentSettings, FindContentOutput>
{
    // Not ICmsAction — this is a read, so no audit trail entry is written.

    /// <summary>Outcome emitted when no content matches.</summary>
    public const string OutcomeNotFound = "notFound";

    private const int MinLimit = 1;
    private const int MaxLimit = 500;

    private readonly IExamineManager _examineManager;
    private readonly IContentTypeService _contentTypeService;
    private readonly IPublishedContentCache _publishedContentCache;
    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IPublishedUrlProvider _urlProvider;
    private readonly ILogger<FindContentAction> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FindContentAction"/> class.
    /// </summary>
    public FindContentAction(
        ActionInfrastructure infrastructure,
        IExamineManager examineManager,
        IContentTypeService contentTypeService,
        IPublishedContentCache publishedContentCache,
        IUmbracoContextFactory umbracoContextFactory,
        IPublishedUrlProvider urlProvider,
        ILogger<FindContentAction> logger)
        : base(infrastructure)
    {
        _examineManager = examineManager;
        _contentTypeService = contentTypeService;
        _publishedContentCache = publishedContentCache;
        _umbracoContextFactory = umbracoContextFactory;
        _urlProvider = urlProvider;
        _logger = logger;
    }

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

        // Picker stores content-type KEYS; Examine indexes only aliases, so resolve here.
        // A key that doesn't resolve is silently dropped — a stale picker selection
        // shouldn't hard-fail the automation, but if every key is stale the caller ends
        // up with an empty alias list and gets zero matches, which is the right signal.
        var contentTypeAliases = ResolveContentTypeAliases(settings.ContentTypes);

        var indexName = settings.IncludeUnpublished
            ? UmbracoConstants.UmbracoIndexes.InternalIndexName
            : UmbracoConstants.UmbracoIndexes.ExternalIndexName;

        if (!_examineManager.TryGetIndex(indexName, out var index))
        {
            // Examine should be registered by default, so this indicates a
            // misconfigured host — treat as an infra/unknown failure, not validation.
            return ActionResult.Failed(
                new InvalidOperationException($"Examine index '{indexName}' is not available."),
                StepRunErrorCategory.Unknown);
        }

        var query = BuildQuery(settings.Name, matchMode, settings.Culture, contentTypeAliases);

        ISearchResults results;
        try
        {
            results = index.Searcher
                .CreateQuery()
                .NativeQuery(query)
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
            .Select(r => Project(r, settings.Culture))
            .ToList();

        await Task.CompletedTask; // keeping the method async-safe in case URL resolution becomes async later

        if (matches.Count == 0)
        {
            return SuccessWithOutcome(OutcomeNotFound, new FindContentOutput());
        }

        return Success(new FindContentOutput
        {
            Matches = matches,
            LimitReached = matches.Count >= settings.Limit,
        });
    }

    /// <summary>
    /// Builds the native Examine query. Visible for tests in the same assembly.
    /// </summary>
    internal static string BuildQuery(
        string name,
        FindContentMatchMode mode,
        string? culture,
        IReadOnlyList<string> contentTypeAliases)
    {
        var nameTerm = BuildNameTerm(name.Trim(), mode);
        var nameFields = BuildNameFieldQuery(nameTerm, culture);

        var query = $"+__IndexType:content +({nameFields})";

        if (contentTypeAliases.Count > 0)
        {
            // Bracketed OR over content type aliases: +(__NodeTypeAlias:a __NodeTypeAlias:b)
            var aliasClause = string.Join(
                " ",
                contentTypeAliases.Select(a => $"{ExamineFieldNames.ItemTypeFieldName}:{QueryParserBase.Escape(a.ToLowerInvariant())}"));
            query += $" +({aliasClause})";
        }

        return query;
    }

    private IReadOnlyList<string> ResolveContentTypeAliases(string? contentTypesCsv)
    {
        if (string.IsNullOrWhiteSpace(contentTypesCsv))
        {
            return [];
        }

        var keys = contentTypesCsv
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
            var contentType = _contentTypeService.Get(key);
            if (contentType is null)
            {
                _logger.LogDebug(
                    "FindContent: content type key {ContentTypeKey} not found, dropping from filter.",
                    key);
                continue;
            }

            aliases.Add(contentType.Alias);
        }

        return aliases;
    }

    private static string BuildNameFieldQuery(string nameTerm, string? culture)
    {
        // When a culture is requested, match the culture-specific name field first and fall
        // back to the invariant nodeName so invariant content still surfaces. When no
        // culture is requested, we match only nodeName — matching every culture-specific
        // field would duplicate results and blur the "invariant only" intent.
        if (string.IsNullOrWhiteSpace(culture))
        {
            return $"nodeName:{nameTerm}";
        }

        var normalisedCulture = culture.Trim().ToLowerInvariant();
        return $"nodeName_{normalisedCulture}:{nameTerm} nodeName:{nameTerm}";
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

    private FindContentMatch Project(ISearchResult result, string? culture)
    {
        var key = TryParseGuid(result, UmbracoExamineFieldNames.NodeKeyFieldName);

        // Pull the culture-specific name if we have one; fall back to invariant.
        string? name = null;
        if (!string.IsNullOrWhiteSpace(culture))
        {
            name = TryGetString(result, $"nodeName_{culture.Trim().ToLowerInvariant()}");
        }
        name ??= TryGetString(result, "nodeName");

        var url = key.HasValue ? TryResolveUrl(key.Value, culture) : null;

        return new FindContentMatch
        {
            ContentKey = key ?? Guid.Empty,
            Name = name,
            ContentTypeAlias = TryGetString(result, ExamineFieldNames.ItemTypeFieldName) ?? string.Empty,
            Level = TryParseInt(result, "level"),
            Path = TryGetString(result, "__Path"),
            Url = url,
            Culture = string.IsNullOrWhiteSpace(culture) ? null : culture,
            CreateDate = TryParseDate(result, "createDate"),
            UpdateDate = TryParseDate(result, "updateDate"),
        };
    }

    private string? TryResolveUrl(Guid key, string? culture)
    {
        // Only published items have URLs — drafts (InternalIndex) return null from the
        // published cache lookup, which is the correct behaviour.
        var content = _publishedContentCache.GetById(key);
        return content?.Url(_urlProvider, culture, UrlMode.Default);
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
