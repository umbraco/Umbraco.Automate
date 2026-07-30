namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that suspends execution and waits for human approval.
/// The workflow resumes when an approval decision is submitted via the API.
/// </summary>
[Action("umbracoAutomate.requestApproval", "Request Approval",
    Description = "Pauses the automation and waits for a human to approve or reject before continuing.",
    Group = "Core",
    Icon = "icon-operator")]
public sealed class RequestApprovalAction : ActionBase<RequestApprovalSettings, ApprovalDecisionOutput>
{
    /// <summary>
    /// The action alias for the request approval action.
    /// </summary>
    public const string ApprovalActionAlias = "umbracoAutomate.requestApproval";

    /// <summary>
    /// The WorkflowCore event name used for approval events.
    /// </summary>
    public const string ApprovalEventName = "approval";

    /// <summary>
    /// Initializes a new instance of the <see cref="RequestApprovalAction"/> class.
    /// </summary>
    public RequestApprovalAction(ActionInfrastructure infrastructure)
        : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<RequestApprovalSettings>();

        var eventKey = $"{context.RunId}:{context.StepId}";

        var output = new ApprovalRequestOutput
        {
            Prompt = settings.Prompt ?? "Approval required to continue.",
            RunId = context.RunId,
            StepId = context.StepId,
            AutomationId = context.AutomationId,
            RequestedUtc = DateTime.UtcNow,
            TimeoutHours = settings.TimeoutHours,
        };

        // The suspend-time payload is the pending-approval record, not the declared
        // ApprovalDecisionOutput — there is no decision yet, and the pending-approvals API reads
        // the prompt from it. The typed WaitForInput overload is bypassed for that reason.
        return Task.FromResult(ActionResult.WaitForInput(ApprovalEventName, eventKey, output));
    }
}
