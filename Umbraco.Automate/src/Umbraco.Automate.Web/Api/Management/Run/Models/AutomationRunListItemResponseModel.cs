using System.ComponentModel.DataAnnotations;
using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Web.Api.Management.Run.Models;

/// <summary>
/// Response model for an automation run in a cross-automation list (e.g. the runs dashboard).
/// Carries the parent automation's name and omits step run detail.
/// </summary>
public sealed class AutomationRunListItemResponseModel
{
    /// <summary>The run ID.</summary>
    [Required]
    public Guid Id { get; set; }

    /// <summary>The automation ID.</summary>
    [Required]
    public Guid AutomationId { get; set; }

    /// <summary>The current name of the parent automation.</summary>
    [Required]
    public string AutomationName { get; set; } = string.Empty;

    /// <summary>The automation version at the time of the run.</summary>
    public int AutomationVersion { get; set; }

    /// <summary>The execution status.</summary>
    [Required]
    public AutomationRunStatus Status { get; set; }

    /// <summary>When the run started.</summary>
    public DateTime? StartedUtc { get; set; }

    /// <summary>When the run completed.</summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>What initiated the run.</summary>
    [Required]
    public string InitiatedBy { get; set; } = string.Empty;

    /// <summary>Optional correlation ID.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>Error message if failed.</summary>
    public string? Error { get; set; }
}
