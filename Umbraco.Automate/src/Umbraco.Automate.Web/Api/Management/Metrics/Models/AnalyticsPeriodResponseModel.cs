using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Models;

/// <summary>
/// The run figures for one range.
/// </summary>
public sealed class AnalyticsPeriodResponseModel
{
    /// <summary>Every run that started in the period.</summary>
    public int Runs { get; set; }

    /// <summary>The completed runs.</summary>
    public int Completed { get; set; }

    /// <summary>The runs a person refused.</summary>
    public int Rejected { get; set; }

    /// <summary>The cancelled runs.</summary>
    public int Cancelled { get; set; }

    /// <summary>The failed runs.</summary>
    public int Failed { get; set; }

    /// <summary>The pending, running and suspended runs.</summary>
    public int InProgress { get; set; }

    /// <summary>(completed + rejected) over all finished runs, 0 to 1. <c>null</c> when nothing finished.</summary>
    public decimal? SuccessRate { get; set; }

    /// <summary>The median run time of finished runs in milliseconds, or <c>null</c> when there are none.</summary>
    public long? MedianDurationMs { get; set; }

    /// <summary>Every day of the period, zeros included.</summary>
    [Required]
    public IEnumerable<AnalyticsDayResponseModel> Days { get; set; } = [];
}
