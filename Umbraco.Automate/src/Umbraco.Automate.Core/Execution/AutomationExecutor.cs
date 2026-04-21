using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Workspaces;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Default <see cref="IAutomationExecutor"/> that compiles an automation definition
/// into a WorkflowCore workflow and starts execution.
/// </summary>
internal sealed class AutomationExecutor : IAutomationExecutor
{
    private readonly IWorkflowHost _workflowHost;
    private readonly IWorkflowRegistry _workflowRegistry;
    private readonly IWorkflowCompiler _compiler;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IWorkspaceService _workspaceService;
    private readonly IRateLimitService _rateLimitService;
    private readonly AutomateMetrics _metrics;
    private readonly ILogger<AutomationExecutor> _logger;

    public AutomationExecutor(
        IWorkflowHost workflowHost,
        IWorkflowRegistry workflowRegistry,
        IWorkflowCompiler compiler,
        IAutomationRunRepository runRepository,
        IWorkspaceService workspaceService,
        IRateLimitService rateLimitService,
        AutomateMetrics metrics,
        ILogger<AutomationExecutor> logger)
    {
        _workflowHost = workflowHost;
        _workflowRegistry = workflowRegistry;
        _compiler = compiler;
        _runRepository = runRepository;
        _workspaceService = workspaceService;
        _rateLimitService = rateLimitService;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<Guid> ExecuteAsync(
        Automation automation,
        string initiatorType,
        string? initiatorId,
        Dictionary<string, object?>? triggerOutputData,
        CancellationToken cancellationToken)
    {
        // Check rate limits before creating the run record.
        await _rateLimitService.CheckRateLimitAsync(automation.Id, cancellationToken);

        // Resolve workspace and service account.
        var workspace = await _workspaceService.GetWorkspaceAsync(automation.WorkspaceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workspace '{automation.WorkspaceId}' not found for automation '{automation.Name}'.");

        // Create the automation run record.
        var run = new AutomationRun
        {
            Id = Guid.NewGuid(),
            AutomationId = automation.Id,
            AutomationVersion = automation.PublishedVersion ?? automation.Version,
            WorkspaceId = workspace.Id,
            ServiceAccountKey = workspace.ServiceAccountKey,
            Status = AutomationRunStatus.Running,
            StartedUtc = DateTime.UtcNow,
            InitiatedBy = initiatorType,
            CorrelationId = initiatorId,
            TriggerData = triggerOutputData is not null
                ? JsonSerializer.Serialize(triggerOutputData, Dispatch.JsonOptions.Default)
                : null,
        };

        await _runRepository.SaveAsync(run, cancellationToken);

        _metrics.RunStarted(automation.Alias);

        // Set the execution context for the current async flow.
        var executionContext = new AutomationExecutionContext
        {
            ServiceAccountKey = workspace.ServiceAccountKey,
            WorkspaceId = workspace.Id,
            WorkspaceName = workspace.Name,
            AutomationId = automation.Id,
            AutomationName = automation.Name,
            RunId = run.Id,
            InitiatorType = initiatorType,
            InitiatorId = initiatorId,
            AllowedConnections = workspace.AllowedConnections.ToList(),
        };

        using var _ = ExecutionContextAccessor.Set(executionContext);

        // Register the workflow definition if not already registered.
        var workflowId = $"automate-{automation.Id}-v{automation.Version}";
        var existing = _workflowRegistry.GetDefinition(workflowId);

        if (existing is null)
        {
            var definition = _compiler.Compile(automation, workflowId);
            _workflowRegistry.RegisterWorkflow(definition);
        }

        // Start the workflow with our data.
        var workflowData = new AutomationWorkflowData
        {
            RunId = run.Id,
            AutomationId = automation.Id,
            AutomationAlias = automation.Alias,
            TriggerOutput = triggerOutputData ?? [],
            StepAliases = automation.Steps
                .Where(s => !string.IsNullOrEmpty(s.Alias))
                .ToDictionary(s => s.Id, s => s.Alias!),
            ExecutionContext = executionContext,
        };

        var instanceId = await _workflowHost.StartWorkflow(workflowId, workflowData);

        // Persist the WorkflowCore instance ID so lifecycle operations (suspend / resume /
        // terminate) can map the run back to the correct workflow.
        run.WorkflowInstanceId = instanceId;
        await _runRepository.SaveAsync(run, cancellationToken);

        _logger.LogInformation(
            "Started workflow {WorkflowId} (instance {InstanceId}) for run {RunId} as service account {ServiceAccountKey}",
            workflowId, instanceId, run.Id, workspace.ServiceAccountKey);

        return run.Id;
    }
}
