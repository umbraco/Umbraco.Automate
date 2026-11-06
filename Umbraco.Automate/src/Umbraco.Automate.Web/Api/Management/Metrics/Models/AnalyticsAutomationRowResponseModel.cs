using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Models;

/// <summary>
/// One automation's run figures for a period.
/// </summary>
public sealed class AnalyticsAutomationRowResponseModel
{
    /// <summary>The automation ID.</summary>
    [Required]
    public Guid AutomationId { get; set; }

    /// <summary>The automation's current name.</summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>The name of the automation's current workspace. Empty when that workspace no longer exists.</summary>
    [Required]
    public string WorkspaceName { get; set; } = string.Empty;

    /// <summary>The runs that started in the period.</summary>
    public int Runs { get; set; }

    /// <summary>The failed runs.</summary>
    public int Failed { get; set; }

    /// <summary>(completed + rejected) over finished runs, 0 to 1. <c>null</c> when none finished.</summary>
    public decimal? SuccessRate { get; set; }

    /// <summary>The average run time of finished runs in milliseconds, or <c>null</c> when none has a recorded duration.</summary>
    public long? AvgDurationMs { get; set; }

    /// <summary>When the latest run in the period started, in UTC.</summary>
    [Required]
    public DateTime LastRunUtc { get; set; }
}
