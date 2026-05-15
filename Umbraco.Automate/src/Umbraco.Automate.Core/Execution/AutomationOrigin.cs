namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Identifies the automation lineage that originated an in-process side effect (e.g. a
/// content save performed by an action). Stamped onto outgoing
/// <see cref="Triggers.TriggerEvent"/>s so receiving automations can detect cycles by
/// looking for their own ID in the chain.
/// </summary>
/// <param name="RunId">The run ID at the head of the chain — the most recent run that caused the side effect.</param>
/// <param name="WorkspaceId">The workspace ID of the most recent run.</param>
/// <param name="AutomationChain">
/// Ordered list of automation IDs in the cascade, oldest to newest. The last entry is the
/// automation that directly caused this side effect; earlier entries are upstream causes.
/// A receiver finding its own ID in the chain means accepting the event would close a cycle.
/// </param>
public sealed record AutomationOrigin(
    Guid RunId,
    Guid WorkspaceId,
    IReadOnlyList<Guid> AutomationChain);
