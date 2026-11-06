using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Models;

/// <summary>
/// One action's step-run figures for a period.
/// </summary>
public sealed class AnalyticsActionRowResponseModel
{
    /// <summary>The action's alias. The client looks up its name.</summary>
    [Required]
    public string ActionAlias { get; set; } = string.Empty;

    /// <summary>The step runs of the action that started in the period.</summary>
    public int Runs { get; set; }

    /// <summary>The failed step runs.</summary>
    public int Failed { get; set; }

    /// <summary>The total retries.</summary>
    public int Retries { get; set; }

    /// <summary>Failed over (completed + failed + rejected) step runs, 0 to 1. <c>null</c> when none of those ran.</summary>
    public decimal? FailureRate { get; set; }

    /// <summary>The average step run time in milliseconds, or <c>null</c> when none has a recorded duration.</summary>
    public long? AvgDurationMs { get; set; }

    /// <summary>
    /// The name of the error category with the most failed step runs, such as <c>Authentication</c>
    /// (the lower value on a tie). <c>null</c> when nothing failed.
    /// </summary>
    public string? TopErrorCategory { get; set; }
}
