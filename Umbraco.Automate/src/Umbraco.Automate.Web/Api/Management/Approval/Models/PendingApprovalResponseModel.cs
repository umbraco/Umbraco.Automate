namespace Umbraco.Automate.Web.Api.Management.Approval.Models;

/// <summary>
/// Response model for a pending approval step.
/// </summary>
public sealed class PendingApprovalResponseModel
{
    /// <summary>The automation run ID.</summary>
    public required Guid RunId { get; init; }

    /// <summary>The step ID within the automation.</summary>
    public required Guid StepId { get; init; }

    /// <summary>The automation ID.</summary>
    public required Guid AutomationId { get; init; }

    /// <summary>The automation name.</summary>
    public required string AutomationName { get; init; }

    /// <summary>The approval prompt message.</summary>
    public string? Prompt { get; init; }

    /// <summary>When the approval was requested.</summary>
    public DateTime? RequestedUtc { get; init; }
}
