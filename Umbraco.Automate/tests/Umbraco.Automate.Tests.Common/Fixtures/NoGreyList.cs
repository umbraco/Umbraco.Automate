using WorkflowCore.Interface;

namespace Umbraco.Automate.Tests.Common.Fixtures;

/// <summary>
/// An <see cref="IGreyList"/> that never holds anything back, for fixtures that publish events.
/// </summary>
/// <remarks>
/// WorkflowCore's poller grey-lists each event it queues for five minutes. In WorkflowCore 3.9.0,
/// an event can be queued twice (by the poller and by a new subscription) while its lock is held,
/// and both copies are then dropped ("Event locked"). The event is left unprocessed but still
/// grey-listed, so a test waits five minutes for it. With this registered after
/// <c>AddWorkflow</c>, the poller re-queues it on its next tick instead. Fixed upstream in
/// WorkflowCore 3.22.0 (danielgerlag/workflow-core#1455); remove this once Automate upgrades.
/// </remarks>
public sealed class NoGreyList : IGreyList
{
    public void Add(string id)
    {
    }

    public void Remove(string id)
    {
    }

    public bool Contains(string id) => false;
}
