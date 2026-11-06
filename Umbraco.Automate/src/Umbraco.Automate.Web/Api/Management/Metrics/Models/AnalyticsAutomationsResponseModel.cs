using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Models;

/// <summary>
/// The automations table: one row per automation that ran in the period.
/// </summary>
public sealed class AnalyticsAutomationsResponseModel
{
    /// <summary>The rows for the period asked for.</summary>
    [Required]
    public IEnumerable<AnalyticsAutomationRowResponseModel> Items { get; set; } = [];

    /// <summary>
    /// The rows for the comparison period. <c>null</c> when no comparison dates were given, or when
    /// the runs of the comparison period have been deleted by run cleanup.
    /// </summary>
    public IEnumerable<AnalyticsAutomationRowResponseModel>? CompareItems { get; set; }
}
