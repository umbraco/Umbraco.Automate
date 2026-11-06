using Umbraco.Automate.Core.Analytics;
using Umbraco.Automate.Web.Api.Management.Metrics.Models;
using Umbraco.Cms.Core.Mapping;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Mapping;

/// <summary>
/// Map definitions for the analytics results (summary, automations and actions).
/// </summary>
public class AnalyticsMapDefinition : IMapDefinition
{
    /// <inheritdoc />
    public void DefineMaps(IUmbracoMapper mapper)
    {
        mapper.Define<AnalyticsSummary, AnalyticsSummaryResponseModel>((_, _) => new AnalyticsSummaryResponseModel(), MapSummary);
        mapper.Define<AnalyticsRows<AutomationAnalyticsRow>, AnalyticsAutomationsResponseModel>(
            (_, _) => new AnalyticsAutomationsResponseModel(), MapAutomations);
        mapper.Define<AnalyticsRows<ActionAnalyticsRow>, AnalyticsActionsResponseModel>(
            (_, _) => new AnalyticsActionsResponseModel(), MapActions);
    }

    // Umbraco.Code.MapAll
    private static void MapSummary(AnalyticsSummary source, AnalyticsSummaryResponseModel target, MapperContext context)
    {
        target.Current = MapPeriod(source.Current);
        target.Compare = source.Compare is null ? null : MapPeriod(source.Compare);
        target.WaitingForApproval = source.WaitingForApproval;
        target.RetainedFrom = source.RetainedFrom;
    }

    // Umbraco.Code.MapAll
    private static void MapAutomations(
        AnalyticsRows<AutomationAnalyticsRow> source,
        AnalyticsAutomationsResponseModel target,
        MapperContext context)
    {
        target.Items = source.Items.Select(MapAutomationRow).ToList();
        target.CompareItems = source.CompareItems?.Select(MapAutomationRow).ToList();
    }

    // Umbraco.Code.MapAll
    private static void MapActions(
        AnalyticsRows<ActionAnalyticsRow> source,
        AnalyticsActionsResponseModel target,
        MapperContext context)
    {
        target.Items = source.Items.Select(MapActionRow).ToList();
        target.CompareItems = source.CompareItems?.Select(MapActionRow).ToList();
    }

    private static AnalyticsPeriodResponseModel MapPeriod(AnalyticsPeriod source) => new()
    {
        Runs = source.Runs,
        Completed = source.Completed,
        Rejected = source.Rejected,
        Cancelled = source.Cancelled,
        Failed = source.Failed,
        InProgress = source.InProgress,
        SuccessRate = source.SuccessRate,
        MedianDurationMs = source.MedianDurationMs,
        Days = source.Days.Select(MapDay).ToList(),
    };

    private static AnalyticsDayResponseModel MapDay(AnalyticsDay source) => new()
    {
        Date = source.Date,
        Completed = source.Completed,
        Rejected = source.Rejected,
        Cancelled = source.Cancelled,
        Failed = source.Failed,
        InProgress = source.InProgress,
    };

    private static AnalyticsAutomationRowResponseModel MapAutomationRow(AutomationAnalyticsRow source) => new()
    {
        AutomationId = source.AutomationId,
        Name = source.Name,
        WorkspaceName = source.WorkspaceName,
        Runs = source.Runs,
        Failed = source.Failed,
        SuccessRate = source.SuccessRate,
        AvgDurationMs = source.AvgDurationMs,
        LastRunUtc = source.LastRunUtc,
    };

    private static AnalyticsActionRowResponseModel MapActionRow(ActionAnalyticsRow source) => new()
    {
        ActionAlias = source.ActionAlias,
        Runs = source.Runs,
        Failed = source.Failed,
        Retries = source.Retries,
        FailureRate = source.FailureRate,
        AvgDurationMs = source.AvgDurationMs,
        TopErrorCategory = source.TopErrorCategory?.ToString(),
    };
}
