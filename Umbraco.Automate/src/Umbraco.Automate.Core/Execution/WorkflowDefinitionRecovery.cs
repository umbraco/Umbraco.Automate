using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Re-registers WorkflowCore definitions for in-flight workflow instances after app restart.
/// WorkflowCore stores definitions in an in-memory registry that is lost on restart, but
/// persisted instances still reference them. Without recovery, the poller logs
/// "Workflow X version Y is not registered" on every tick.
/// </summary>
internal sealed class WorkflowDefinitionRecovery
{
    private static readonly Regex WorkflowIdPattern = new(
        @"^automate-(?<id>[0-9a-f\-]{36})-v(?<version>\d+)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly IPersistenceProvider _persistence;
    private readonly IWorkflowRegistry _registry;
    private readonly IWorkflowCompiler _compiler;
    private readonly IAutomationService _automationService;
    private readonly ILogger<WorkflowDefinitionRecovery> _logger;

    public WorkflowDefinitionRecovery(
        IPersistenceProvider persistence,
        IWorkflowRegistry registry,
        IWorkflowCompiler compiler,
        IAutomationService automationService,
        ILogger<WorkflowDefinitionRecovery> logger)
    {
        _persistence = persistence;
        _registry = registry;
        _compiler = compiler;
        _automationService = automationService;
        _logger = logger;
    }

    /// <summary>
    /// Finds all in-flight workflow instances and registers their definitions.
    /// Instances whose definitions cannot be recovered are terminated to prevent
    /// WorkflowCore's poller from logging "not registered" errors indefinitely.
    /// </summary>
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var instancesByDefinition = await GetInFlightInstancesByDefinitionAsync(cancellationToken);
        if (instancesByDefinition.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Recovering {Count} workflow definition(s) for in-flight instances", instancesByDefinition.Count);

        foreach (var (key, instances) in instancesByDefinition)
        {
            if (_registry.IsRegistered(key.WorkflowId, key.Version))
            {
                continue;
            }

            var recovered = await TryRegisterDefinitionAsync(key.WorkflowId, key.Version, cancellationToken);
            if (!recovered)
            {
                await TerminateOrphanedInstancesAsync(instances, key.WorkflowId, cancellationToken);
            }
        }
    }

    private async Task<Dictionary<(string WorkflowId, int Version), List<WorkflowInstance>>> GetInFlightInstancesByDefinitionAsync(
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(string, int), List<WorkflowInstance>>();

        // Query runnable and suspended instances — these are the ones WorkflowCore's poller will try to process.
        foreach (var status in new[] { WorkflowStatus.Runnable, WorkflowStatus.Suspended })
        {
            var instances = await _persistence.GetWorkflowInstances(
                status, type: null!, createdFrom: null, createdTo: null, skip: 0, take: int.MaxValue);

            foreach (var instance in instances)
            {
                var key = (instance.WorkflowDefinitionId, instance.Version);
                if (!result.TryGetValue(key, out var list))
                {
                    list = [];
                    result[key] = list;
                }

                list.Add(instance);
            }
        }

        return result;
    }

    /// <returns><c>true</c> if the definition was successfully registered; otherwise <c>false</c>.</returns>
    private async Task<bool> TryRegisterDefinitionAsync(string workflowId, int version, CancellationToken cancellationToken)
    {
        var match = WorkflowIdPattern.Match(workflowId);
        if (!match.Success)
        {
            _logger.LogWarning("Cannot parse automation ID from workflow definition '{WorkflowId}', skipping recovery", workflowId);
            return false;
        }

        var automationId = Guid.Parse(match.Groups["id"].Value);
        var automationVersion = int.Parse(match.Groups["version"].Value);

        // Try the version snapshot first (exact version that was running), fall back to current automation.
        var automation = await _automationService.GetAutomationVersionSnapshotAsync(
            automationId, automationVersion, cancellationToken);

        automation ??= await _automationService.GetAutomationAsync(automationId, cancellationToken);

        if (automation is null)
        {
            _logger.LogWarning(
                "Automation {AutomationId} not found, cannot recover workflow definition '{WorkflowId}'",
                automationId, workflowId);
            return false;
        }

        try
        {
            var definition = _compiler.Compile(automation, workflowId);
            _registry.RegisterWorkflow(definition);

            _logger.LogInformation(
                "Recovered workflow definition '{WorkflowId}' for automation '{AutomationName}'",
                workflowId, automation.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to recover workflow definition '{WorkflowId}' for automation {AutomationId}",
                workflowId, automationId);
            return false;
        }
    }

    private async Task TerminateOrphanedInstancesAsync(
        List<WorkflowInstance> instances, string workflowId, CancellationToken cancellationToken)
    {
        foreach (var instance in instances)
        {
            try
            {
                instance.Status = WorkflowStatus.Terminated;
                instance.CompleteTime = DateTime.UtcNow;
                await _persistence.PersistWorkflow(instance, cancellationToken);

                _logger.LogWarning(
                    "Terminated orphaned workflow instance '{InstanceId}' for unrecoverable definition '{WorkflowId}'",
                    instance.Id, workflowId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to terminate orphaned workflow instance '{InstanceId}' for definition '{WorkflowId}'",
                    instance.Id, workflowId);
            }
        }
    }
}
