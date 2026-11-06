using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Models;

/// <summary>
/// The actions table: one row per action that ran in the period.
/// </summary>
public sealed class AnalyticsActionsResponseModel
{
    /// <summary>The rows for the period asked for.</summary>
    [Required]
    public IEnumerable<AnalyticsActionRowResponseModel> Items { get; set; } = [];

    /// <summary>
    /// The rows for the comparison period. <c>null</c> when no comparison dates were given, or when
    /// the runs of the comparison period have been deleted by run cleanup.
    /// </summary>
    public IEnumerable<AnalyticsActionRowResponseModel>? CompareItems { get; set; }
}
