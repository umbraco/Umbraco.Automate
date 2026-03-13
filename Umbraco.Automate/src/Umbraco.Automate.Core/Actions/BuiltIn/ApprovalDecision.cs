namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A human decision on a pending approval step.
/// </summary>
public sealed class ApprovalDecision
{
    /// <summary>
    /// Gets or sets the decision outcome.
    /// </summary>
    public required ApprovalOutcome Outcome { get; init; }

    /// <summary>
    /// Gets or sets an optional comment from the approver.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Gets or sets the user key of the approver.
    /// </summary>
    public Guid? ApprovedByUserKey { get; init; }

    /// <summary>
    /// Gets or sets the UTC time the decision was made.
    /// </summary>
    public DateTime DecisionUtc { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// The outcome of an approval decision.
/// </summary>
public enum ApprovalOutcome
{
    /// <summary>The step is approved to continue.</summary>
    Approved = 0,

    /// <summary>The step is rejected and should fail.</summary>
    Rejected = 1,
}
