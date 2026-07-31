namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// The output data emitted by <see cref="RequestApprovalAction"/> once a human decision has been
/// submitted. This is the declared output type of the action, so it is the shape the backoffice
/// binding picker offers and the shape downstream steps bind against — for example
/// <c>${ steps.approval.outcome }</c> in an If step's condition.
/// </summary>
/// <remarks>
/// The suspend-time payload is <see cref="ApprovalRequestOutput"/> instead: while the step is
/// waiting there is no decision to report, and the pending-approvals API reads the prompt from it.
/// Downstream steps only ever run after the decision, so this type is the one worth describing.
/// </remarks>
public sealed class ApprovalDecisionOutput
{
    /// <summary>
    /// Gets a value indicating whether the approval was granted. This is the field to branch on:
    /// <c>${ steps.approval.approved }</c> equals <c>true</c> reads better in a condition than a
    /// string comparison, and cannot be broken by a typo in an outcome name.
    /// </summary>
    public bool Approved { get; init; }

    /// <summary>
    /// Gets the decision outcome as its name — <c>Approved</c> or <c>Rejected</c>.
    /// Retained alongside <see cref="Approved"/> so existing conditions and logs that read
    /// <c>outcome</c> keep working, and so a future third outcome has somewhere to live.
    /// </summary>
    /// <remarks>
    /// Deliberately a string rather than <see cref="ApprovalOutcome"/>. Step output is serialised
    /// with <c>JsonOptions.Default</c>, which has no string-enum converter, so an enum property
    /// would surface in bindings as its numeric value (<c>0</c>/<c>1</c>) and force conditions to
    /// compare against a magic number.
    /// </remarks>
    public required string Outcome { get; init; }

    /// <summary>
    /// Gets the optional comment left by the approver.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Gets the user key of the approver.
    /// </summary>
    public Guid? ApprovedByUserKey { get; init; }

    /// <summary>
    /// Gets the UTC time the decision was made.
    /// </summary>
    public DateTime DecisionUtc { get; init; }
}
