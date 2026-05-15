namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Carries the execution identity and context for an automation run.
/// Available to actions via <see cref="Actions.ActionContext.ExecutionContext"/>.
/// </summary>
public sealed class AutomationExecutionContext
{
    /// <summary>
    /// Gets the service account key (<c>UserKind.Api</c> user) that this automation executes as.
    /// </summary>
    public required Guid ServiceAccountKey { get; init; }

    /// <summary>
    /// Gets the workspace ID.
    /// </summary>
    public required Guid WorkspaceId { get; init; }

    /// <summary>
    /// Gets the workspace name.
    /// </summary>
    public required string WorkspaceName { get; init; }

    /// <summary>
    /// Gets the automation ID.
    /// </summary>
    public required Guid AutomationId { get; init; }

    /// <summary>
    /// Gets the automation name.
    /// </summary>
    public required string AutomationName { get; init; }

    /// <summary>
    /// Gets the run ID.
    /// </summary>
    public required Guid RunId { get; init; }

    /// <summary>
    /// Gets the type of initiator (e.g. "user", "scheduled", "webhook", "ai-agent").
    /// </summary>
    public required string InitiatorType { get; init; }

    /// <summary>
    /// Gets the optional initiator identifier (e.g. user email, webhook alias).
    /// </summary>
    public string? InitiatorId { get; init; }

    /// <summary>
    /// Gets the connection IDs allowed in this workspace.
    /// Used for auto-resolving connections by type when a step has no explicit connection ID.
    /// </summary>
    public required IReadOnlyList<Guid> AllowedConnections { get; init; }

    /// <summary>
    /// Gets the upstream automation chain for this run — the ordered list of automation IDs
    /// (oldest to newest) that caused this run, NOT including the current run's automation.
    /// Empty for runs started by a non-automation source. The middleware appends the current
    /// automation when pushing the ambient origin, so events dispatched from within this run
    /// carry <c>OriginChain ∪ { this.AutomationId }</c>.
    /// </summary>
    public IReadOnlyList<Guid> OriginChain { get; init; } = [];

    /// <summary>
    /// Formats the <c>PerformingDetails</c> string for the CMS audit trail.
    /// </summary>
    public string FormatPerformingDetails()
        => $"Umbraco Automate: '{AutomationName}' (Run {RunId})";

    /// <summary>
    /// Formats the <c>EventDetails</c> JSON string for the CMS audit trail.
    /// </summary>
    public string FormatEventDetails(Guid stepId)
        => $$"""{"automationId":"{{AutomationId}}","runId":"{{RunId}}","stepId":"{{stepId}}","workspace":"{{WorkspaceName}}","initiatedBy":"{{InitiatorType}}:{{InitiatorId ?? "unknown"}}"}""";
}
