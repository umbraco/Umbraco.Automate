namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// The output data emitted by <see cref="RequestApprovalAction"/> when a step suspends
/// to wait for a human decision. Shared by the write side (the action) and the read side
/// (the pending-approvals API) so the two stay in sync.
/// </summary>
public sealed class ApprovalRequestOutput
{
    /// <summary>
    /// Gets the approval prompt message shown to approvers.
    /// </summary>
    public required string Prompt { get; init; }

    /// <summary>
    /// Gets the id of the run the approval belongs to.
    /// </summary>
    public Guid RunId { get; init; }

    /// <summary>
    /// Gets the id of the step awaiting approval.
    /// </summary>
    public Guid StepId { get; init; }

    /// <summary>
    /// Gets the id of the automation the approval belongs to.
    /// </summary>
    public Guid AutomationId { get; init; }

    /// <summary>
    /// Gets the UTC time the approval was requested.
    /// </summary>
    public DateTime RequestedUtc { get; init; }

    /// <summary>
    /// Gets the optional timeout, in hours, after which the step auto-rejects.
    /// </summary>
    public int? TimeoutHours { get; init; }
}
