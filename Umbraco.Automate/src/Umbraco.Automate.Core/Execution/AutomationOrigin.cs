namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Identifies the automation run that originated an in-process side effect (e.g. a content
/// save performed by an action). Stamped onto outgoing <see cref="Triggers.TriggerEvent"/>s
/// so receiving automations can opt out of looping on their own changes and so a global
/// chain-depth backstop can drop runaway cascades.
/// </summary>
/// <param name="RunId">The run ID that originated the side effect.</param>
/// <param name="AutomationId">The automation ID the run belongs to.</param>
/// <param name="WorkspaceId">The workspace ID the automation belongs to.</param>
/// <param name="ChainDepth">
/// The number of automation hops accumulated up to and including the current run. The first
/// run started by a non-automation trigger has depth 0; events it dispatches carry depth 1;
/// runs started from those events have depth 1; and so on.
/// </param>
public sealed record AutomationOrigin(
    Guid RunId,
    Guid AutomationId,
    Guid WorkspaceId,
    int ChainDepth);
