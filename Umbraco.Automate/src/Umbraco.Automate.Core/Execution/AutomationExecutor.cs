using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Configuration;
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
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IOptions<ExecutionOptions> _executionOptions;
    private readonly AutomateMetrics _metrics;
    private readonly ILogger<AutomationExecutor> _logger;

    public AutomationExecutor(
        IWorkflowHost workflowHost,
        IWorkflowRegistry workflowRegistry,
        IWorkflowCompiler compiler,
        IAutomationRunRepository runRepository,
        IWorkspaceService workspaceService,
        IRateLimitService rateLimitService,
        ICircuitBreakerService circuitBreaker,
        ConditionEvaluator conditionEvaluator,
        IOptions<ExecutionOptions> executionOptions,
        AutomateMetrics metrics,
        ILogger<AutomationExecutor> logger)
    {
        _workflowHost = workflowHost;
        _workflowRegistry = workflowRegistry;
        _compiler = compiler;
        _runRepository = runRepository;
        _workspaceService = workspaceService;
        _rateLimitService = rateLimitService;
        _circuitBreaker = circuitBreaker;
        _conditionEvaluator = conditionEvaluator;
        _executionOptions = executionOptions;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<Guid> ExecuteAsync(
        Automation automation,
        string initiatorType,
        string? initiatorId,
        Dictionary<string, object?>? triggerOutputData,
        CancellationToken cancellationToken,
        IReadOnlyList<Guid>? originChain = null)
    {
        // Check rate limits before creating the run record.
        await _rateLimitService.CheckRateLimitAsync(automation.Id, cancellationToken);

        // Circuit breaker gate: an auto-disabled automation does not run, except for permitted
        // interactive test runs. Quiet skip (no run record, no workflow) — the trigger dispatch
        // path ignores the return value; interactive Web controllers gate earlier and return 409.
        if (!await _circuitBreaker.IsRunAllowedAsync(automation.Id, initiatorType, cancellationToken))
        {
            _logger.LogDebug(
                "Run skipped — circuit open for automation {AutomationId} ({AutomationAlias}), initiator {Initiator}",
                automation.Id, automation.Alias, initiatorType);
            return Guid.Empty;
        }

        // Resolve workspace and service account.
        var workspace = await _workspaceService.GetWorkspaceAsync(automation.WorkspaceId, cancellationToken)
            ?? throw new InvalidOperationException($"Workspace '{automation.WorkspaceId}' not found for automation '{automation.Name}'.");

        // Serialized once and reused: it is both the run's persisted TriggerData and the size
        // measurement that decides whether the workflow data carries the payload inline or only
        // a marker referencing this run.
        var triggerJson = triggerOutputData is not null
            ? JsonSerializer.Serialize(triggerOutputData, Dispatch.JsonOptions.Default)
            : null;

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
            TriggerData = triggerJson,
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
            OriginChain = originChain ?? [],
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

        // Start the workflow with our data. Trigger output starts out inline so the trigger-edge
        // filter below binds against the in-memory payload (no database read); it is swapped for
        // a marker, if oversized, immediately afterwards — before the data reaches WorkflowCore.
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

        // Trigger-edge filters can't be wired as ValueOutcomes (the trigger isn't a
        // WorkflowCore step), so evaluate them here against trigger data only.
        // If the entry edge's filter rejects the run, complete it without starting
        // the workflow at all.
        if (!EvaluateTriggerFilter(automation, workflowData))
        {
            run.Status = AutomationRunStatus.Completed;
            run.CompletedUtc = DateTime.UtcNow;
            await _runRepository.SaveAsync(run, cancellationToken);

            _metrics.RunCompleted(automation.Alias);

            _logger.LogInformation(
                "Skipped run {RunId} for automation {AutomationName}: trigger-edge filter evaluated to false",
                run.Id, automation.Name);

            return run.Id;
        }

        // A large trigger payload would be re-serialized into the WorkflowCore instance blob on
        // every execution pass, so only a marker referencing this run (whose TriggerData above is
        // written once) goes into the workflow data — binding evaluation hydrates it on demand via
        // StepOutputHydrationCache, exactly as it does for offloaded step outputs.
        if (triggerOutputData is not null && triggerJson is not null)
        {
            workflowData.TriggerOutput = StepOutputReference.CreateInlineOrTriggerMarker(
                triggerOutputData, triggerJson, run.Id, _executionOptions.Value.MaxInlineOutputBytes);
        }

        var instanceId = await _workflowHost.StartWorkflow(workflowId, workflowData);

        // Scoped update so lifecycle operations (suspend / resume / terminate) can
        // map the run back to its workflow. A full SaveAsync here would overwrite
        // Status / CompletedUtc / Error with the stale local run (still Running)
        // and race with RunFinalizer when the first step is WaitForEvent or
        // fast-completing.
        run.WorkflowInstanceId = instanceId;
        await _runRepository.SetWorkflowInstanceIdAsync(run.Id, instanceId, cancellationToken);

        _logger.LogInformation(
            "Started workflow {WorkflowId} (instance {InstanceId}) for run {RunId} as service account {ServiceAccountKey}",
            workflowId, instanceId, run.Id, workspace.ServiceAccountKey);

        return run.Id;
    }

    /// <summary>
    /// Evaluates filters on connections originating from the trigger (Guid.Empty source)
    /// against trigger data only. Returns true if the run should proceed:
    /// — no trigger-edge filter exists, or
    /// — at least one trigger-edge filter passes.
    /// Returns false only when every trigger-edge has a filter and all of them fail.
    /// </summary>
    private bool EvaluateTriggerFilter(Automation automation, AutomationWorkflowData workflowData)
    {
        var triggerEdges = automation.Connections
            .Where(c => c.SourceStepId == Guid.Empty)
            .ToList();

        if (triggerEdges.Count == 0)
        {
            return true;
        }

        var bindingData = BindingDataBuilder.Build(workflowData);

        var passed = false;
        var any = false;
        foreach (var edge in triggerEdges)
        {
            if (edge.Filter is null)
            {
                // An unfiltered trigger edge always proceeds.
                return true;
            }

            any = true;
            if (_conditionEvaluator.Evaluate(edge.Filter, bindingData))
            {
                passed = true;
                break;
            }
        }

        return !any || passed;
    }
}
