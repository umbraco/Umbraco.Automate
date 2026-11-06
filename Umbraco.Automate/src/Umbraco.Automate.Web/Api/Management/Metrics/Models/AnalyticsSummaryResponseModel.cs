using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Models;

/// <summary>
/// The headline numbers and the daily chart for a range, with an optional comparison.
/// </summary>
public sealed class AnalyticsSummaryResponseModel
{
    /// <summary>The period asked for.</summary>
    [Required]
    public AnalyticsPeriodResponseModel Current { get; set; } = new();

    /// <summary>
    /// The comparison period. <c>null</c> when no comparison dates were given, or when the runs of
    /// the comparison period have been deleted by run cleanup.
    /// </summary>
    public AnalyticsPeriodResponseModel? Compare { get; set; }

    /// <summary>The runs waiting for approval right now. Ignores the range.</summary>
    public int WaitingForApproval { get; set; }

    /// <summary>The oldest day still kept, as <c>yyyy-MM-dd</c>. <c>null</c> when age-based run cleanup is off.</summary>
    public DateOnly? RetainedFrom { get; set; }
}
