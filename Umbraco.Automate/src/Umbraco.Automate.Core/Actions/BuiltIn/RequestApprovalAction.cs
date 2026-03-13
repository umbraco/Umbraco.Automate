namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that suspends execution and waits for human approval.
/// The workflow resumes when an approval decision is submitted via the API.
/// </summary>
[Action("umbracoAutomate.requestApproval", "Request Approval")]
public sealed class RequestApprovalAction : ActionBase<RequestApprovalSettings>
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
    public override string? Description => "Pauses the automation and waits for a human to approve or reject before continuing.";

    /// <inheritdoc />
    public override string? Group => "Core";

    /// <inheritdoc />
    public override string? Icon => "icon-operator";

    /// <inheritdoc />
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<RequestApprovalSettings>();

        var eventKey = $"{context.RunId}:{context.StepId}";

        var output = new
        {
            Prompt = settings.Prompt ?? "Approval required to continue.",
            context.RunId,
            context.StepId,
            context.AutomationId,
            RequestedUtc = DateTime.UtcNow,
            settings.TimeoutHours,
        };

        return Task.FromResult(ActionResult.WaitForInput(ApprovalEventName, eventKey, output));
    }
}
