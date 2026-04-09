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
    /// </summary>
    public async Task RecoverAsync(CancellationToken cancellationToken)
    {
        var definitionIds = await GetInFlightDefinitionIdsAsync(cancellationToken);
        if (definitionIds.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Recovering {Count} workflow definition(s) for in-flight instances", definitionIds.Count);

        foreach (var (workflowId, version) in definitionIds)
        {
            if (_registry.IsRegistered(workflowId, version))
            {
                continue;
            }

            await TryRegisterDefinitionAsync(workflowId, version, cancellationToken);
        }
    }

    private async Task<HashSet<(string WorkflowId, int Version)>> GetInFlightDefinitionIdsAsync(
        CancellationToken cancellationToken)
    {
        var result = new HashSet<(string, int)>();

        // Query runnable and suspended instances — these are the ones WorkflowCore's poller will try to process.
        foreach (var status in new[] { WorkflowStatus.Runnable, WorkflowStatus.Suspended })
        {
            var instances = await _persistence.GetWorkflowInstances(
                status, type: null!, createdFrom: null, createdTo: null, skip: 0, take: int.MaxValue);

            foreach (var instance in instances)
            {
                result.Add((instance.WorkflowDefinitionId, instance.Version));
            }
        }

        return result;
    }

    private async Task TryRegisterDefinitionAsync(string workflowId, int version, CancellationToken cancellationToken)
    {
        var match = WorkflowIdPattern.Match(workflowId);
        if (!match.Success)
        {
            _logger.LogWarning("Cannot parse automation ID from workflow definition '{WorkflowId}', skipping recovery", workflowId);
            return;
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
            return;
        }

        try
        {
            var definition = _compiler.Compile(automation, workflowId);
            _registry.RegisterWorkflow(definition);

            _logger.LogInformation(
                "Recovered workflow definition '{WorkflowId}' for automation '{AutomationName}'",
                workflowId, automation.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to recover workflow definition '{WorkflowId}' for automation {AutomationId}",
                workflowId, automationId);
        }
    }
}
