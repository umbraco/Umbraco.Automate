using Umbraco.Cms.Core;

namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// Read-only run analytics for the backoffice: numbers over a range of the user's local days.
/// </summary>
public interface IAutomateAnalyticsService
{
    /// <summary>
    /// Gets the headline numbers and daily counts for the range, and for the comparison range when
    /// there is one. Fails with a status, and never touches the database, when the query is invalid.
    /// </summary>
    Task<Attempt<AnalyticsSummary?, AnalyticsQueryStatus>> GetSummaryAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one row per automation that ran in the range, and for the comparison range when there is
    /// one. Fails with a status, and never touches the database, when the query is invalid.
    /// </summary>
    Task<Attempt<AnalyticsRows<AutomationAnalyticsRow>?, AnalyticsQueryStatus>> GetAutomationsAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one row per action that ran in the range, and for the comparison range when there is
    /// one. Fails with a status, and never touches the database, when the query is invalid.
    /// </summary>
    Task<Attempt<AnalyticsRows<ActionAnalyticsRow>?, AnalyticsQueryStatus>> GetActionsAsync(
        AnalyticsQuery query,
        CancellationToken cancellationToken = default);
}
