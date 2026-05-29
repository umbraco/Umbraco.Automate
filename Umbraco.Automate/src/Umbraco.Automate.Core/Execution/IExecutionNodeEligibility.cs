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

    /// <summary>
    /// As <see cref="CanExecuteWorkflows()"/>, but on <c>false</c> also outputs a
    /// human-readable reason and remediation hint explaining why this node is not
    /// consuming workflow work. <paramref name="reason"/> is <c>null</c> when the
    /// method returns <c>true</c>.
    /// </summary>
    bool CanExecuteWorkflows(out string? reason);
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

    public bool CanExecuteWorkflows() => CanExecuteWorkflows(out _);

    public bool CanExecuteWorkflows(out string? reason)
    {
        if (_executionOptions.Value.Mode == ExecutionMode.Distributed)
        {
            reason = null;
            return true;
        }

        // SchedulerOnly: only the elected publisher (or Single in single-instance) processes work.
        // Crucially, ServerRole.Unknown — the transient role during startup before election —
        // returns false, so we don't claim and lose messages while the role is still settling.
        var role = _serverRoleAccessor.CurrentServerRole;
        if (role is ServerRole.Single or ServerRole.SchedulingPublisher)
        {
            reason = null;
            return true;
        }

        reason = role switch
        {
            ServerRole.Unknown =>
                "ServerRole is Unknown — this node has not been elected. This is usually because Umbraco " +
                "cannot determine its application URL (so the server-registration job never runs): set " +
                "Umbraco:CMS:WebRouting:ApplicationUrlDetection to 'FirstRequest', set " +
                "Umbraco:CMS:WebRouting:UmbracoApplicationUrl explicitly, or for a single-instance install set " +
                "Umbraco:CMS:Global:DisableElectionForSingleServer to true.",
            ServerRole.Subscriber =>
                "ServerRole is Subscriber — in SchedulerOnly execution mode only the elected SchedulingPublisher " +
                "(or a Single server) consumes workflow work. This is expected on subscriber nodes.",
            _ =>
                $"ServerRole '{role}' is not eligible to consume workflow work in SchedulerOnly execution mode.",
        };
        return false;
    }
}
