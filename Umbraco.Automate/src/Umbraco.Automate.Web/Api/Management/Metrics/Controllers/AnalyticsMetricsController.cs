using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Analytics;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Web.Api.Management.Metrics.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Security.Authorization;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Controllers;

/// <summary>
/// Controller for run analytics: the summary tiles and chart, and the automations and actions tables.
/// Results are scoped to workspaces the current user has access to.
/// </summary>
[ApiVersion("1.0")]
public class AnalyticsMetricsController : MetricsControllerBase
{
    private readonly IAutomateAnalyticsService _analyticsService;
    private readonly IWorkspaceService _workspaceService;
    private readonly IAuthorizationHelper _authorizationHelper;
    private readonly IUmbracoMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="AnalyticsMetricsController"/> class.
    /// </summary>
    public AnalyticsMetricsController(
        IAutomateAnalyticsService analyticsService,
        IWorkspaceService workspaceService,
        IAuthorizationHelper authorizationHelper,
        IUmbracoMapper mapper)
    {
        _analyticsService = analyticsService;
        _workspaceService = workspaceService;
        _authorizationHelper = authorizationHelper;
        _mapper = mapper;
    }

    /// <summary>
    /// Get the headline numbers and the daily chart for a range, with an optional comparison.
    /// </summary>
    /// <param name="query">The range, comparison range, time zone and workspace filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [HttpGet("analytics/summary")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AnalyticsSummaryResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] AnalyticsQueryRequestModel query,
        CancellationToken cancellationToken = default)
    {
        var attempt = await _analyticsService.GetSummaryAsync(await BuildQueryAsync(query, cancellationToken), cancellationToken);
        return attempt.Success
            ? Ok(_mapper.Map<AnalyticsSummaryResponseModel>(attempt.Result!))
            : InvalidQuery(attempt.Status);
    }

    /// <summary>
    /// Get one row per automation that ran in the range, with an optional comparison.
    /// </summary>
    /// <param name="query">The range, comparison range, time zone and workspace filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [HttpGet("analytics/automations")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AnalyticsAutomationsResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAutomations(
        [FromQuery] AnalyticsQueryRequestModel query,
        CancellationToken cancellationToken = default)
    {
        var attempt = await _analyticsService.GetAutomationsAsync(await BuildQueryAsync(query, cancellationToken), cancellationToken);
        return attempt.Success
            ? Ok(_mapper.Map<AnalyticsAutomationsResponseModel>(attempt.Result!))
            : InvalidQuery(attempt.Status);
    }

    /// <summary>
    /// Get one row per action that ran in the range, with an optional comparison.
    /// </summary>
    /// <param name="query">The range, comparison range, time zone and workspace filter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    [HttpGet("analytics/actions")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(typeof(AnalyticsActionsResponseModel), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetActions(
        [FromQuery] AnalyticsQueryRequestModel query,
        CancellationToken cancellationToken = default)
    {
        var attempt = await _analyticsService.GetActionsAsync(await BuildQueryAsync(query, cancellationToken), cancellationToken);
        return attempt.Success
            ? Ok(_mapper.Map<AnalyticsActionsResponseModel>(attempt.Result!))
            : InvalidQuery(attempt.Status);
    }

    private async Task<AnalyticsQuery> BuildQueryAsync(AnalyticsQueryRequestModel query, CancellationToken cancellationToken)
        => new()
        {
            From = query.From,
            To = query.To,
            CompareFrom = query.CompareFrom,
            CompareTo = query.CompareTo,
            TimeZoneId = query.TimeZone,
            WorkspaceIds = await ResolveAccessibleWorkspacesAsync(_workspaceService, _authorizationHelper, query.WorkspaceId, cancellationToken),
        };

    private IActionResult InvalidQuery(AnalyticsQueryStatus status)
        => OperationStatusResult(status, problem => BadRequest(problem
            .WithTitle("Invalid analytics query")
            .WithDetail(status switch
            {
                AnalyticsQueryStatus.UnknownTimeZone => "The time zone isn't a known IANA name.",
                AnalyticsQueryStatus.RangeBackwards => "The range starts after it ends.",
                AnalyticsQueryStatus.RangeTooLong => "The range is longer than 366 days.",
                AnalyticsQueryStatus.IncompleteCompare => "Only one of the two comparison dates was given.",
                AnalyticsQueryStatus.CompareLengthMismatch => "The comparison range isn't the same length as the range.",
                AnalyticsQueryStatus.DateOutOfRange => "Dates must be between 2000-01-01 and 2999-12-31.",
                _ => "The query is not valid.",
            })
            .Build()));
}
