using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Cms.Core.Sync;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Determines whether the current node is eligible to consume workflow-execution work
/// (trigger events, workflow queue items, workflow event items) from the outbox.
/// </summary>
/// <remarks>
/// In <see cref="ExecutionMode.SchedulerOnly"/> only the elected <c>Single</c> or
/// <c>SchedulingPublisher</c> role processes workflows. Non-eligible nodes (Subscribers,
/// or any node before role election completes) must not claim outbox messages — otherwise
/// the elected node never sees them.
/// </remarks>
public interface IExecutionNodeEligibility
{
    /// <summary>
    /// Returns <c>true</c> if this node should currently consume workflow-execution work.
    /// </summary>
    bool CanExecuteWorkflows();
}

internal sealed class ExecutionNodeEligibility : IExecutionNodeEligibility
{
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IOptions<ExecutionOptions> _executionOptions;

    public ExecutionNodeEligibility(
        IServerRoleAccessor serverRoleAccessor,
        IOptions<ExecutionOptions> executionOptions)
    {
        _serverRoleAccessor = serverRoleAccessor;
        _executionOptions = executionOptions;
    }

    public bool CanExecuteWorkflows()
    {
        if (_executionOptions.Value.Mode == ExecutionMode.Distributed)
        {
            return true;
        }

        // SchedulerOnly: only the elected publisher (or Single in single-instance) processes work.
        // Crucially, ServerRole.Unknown — the transient role during startup before election —
        // returns false, so we don't claim and lose messages while the role is still settling.
        return _serverRoleAccessor.CurrentServerRole is ServerRole.Single
            or ServerRole.SchedulingPublisher;
    }
}
