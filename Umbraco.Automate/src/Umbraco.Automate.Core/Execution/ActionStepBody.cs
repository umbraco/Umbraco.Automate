using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Expressions;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// WorkflowCore step body that executes an Umbraco Automate action within the middleware pipeline.
/// One instance is created per step in the compiled workflow.
/// </summary>
internal sealed class ActionStepBody : StepBodyAsync
{
    private readonly StepConfiguration _stepConfig;
    private readonly IAction _action;
    private readonly ActionMiddlewarePipeline _pipeline;
    private readonly ExpressionEvaluator _expressionEvaluator;
    private readonly SettingsExpressionResolver _settingsExpressionResolver;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IConnectionService _connectionService;
    private readonly AutomateMetrics _metrics;
    private readonly ILogger<ActionStepBody> _logger;

    public ActionStepBody(
        StepConfiguration stepConfig,
        IAction action,
        ActionMiddlewarePipeline pipeline,
        ExpressionEvaluator expressionEvaluator,
        SettingsExpressionResolver settingsExpressionResolver,
        IAutomationRunRepository runRepository,
        IConnectionService connectionService,
        AutomateMetrics metrics,
        ILogger<ActionStepBody> logger)
    {
        _stepConfig = stepConfig;
        _action = action;
        _pipeline = pipeline;
        _expressionEvaluator = expressionEvaluator;
        _settingsExpressionResolver = settingsExpressionResolver;
        _runRepository = runRepository;
        _connectionService = connectionService;
        _metrics = metrics;
        _logger = logger;
    }

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var cancellationToken = context.CancellationToken;

        // Resume path: workflow is resuming after WaitForEvent with the event data.
        if (context.Item is not null)
        {
            return await HandleResumeAsync(context, data, cancellationToken);
        }

        // Normal execution path.
        return await HandleExecuteAsync(context, data, cancellationToken);
    }

    private async Task<ExecutionResult> HandleExecuteAsync(
        IStepExecutionContext context,
        AutomationWorkflowData data,
        CancellationToken cancellationToken)
    {
        // Build expression data context: trigger output + all prior step outputs.
        var expressionData = BuildExpressionData(data);

        // Resolve input mappings via expressions.
        var resolvedInputs = ResolveInputMappings(_stepConfig.InputMappings, expressionData);

        // Resolve settings for the action (deserialize, resolve $config refs, validate).
        object? settings = null;
        if (_action.SettingsType is not null && _stepConfig.Settings.Count > 0)
        {
            settings = _action.ResolveSettings(_stepConfig.Settings);
        }

        // Evaluate ${ } expressions in settings properties marked with SupportsExpressions.
        if (settings is not null)
        {
            _settingsExpressionResolver.ResolveExpressions(settings, expressionData);
        }

        // Resolve connection for this step (if configured).
        ConfiguredConnection? connection = null;
        if (_stepConfig.ConnectionId is { } connectionId)
        {
            connection = await _connectionService.GetConfiguredConnectionAsync(connectionId, cancellationToken);
        }

        // Create action context.
        var actionContext = new ActionContext
        {
            AutomationId = data.AutomationId,
            RunId = data.RunId,
            StepId = _stepConfig.Id,
            ActionAlias = _action.Alias,
            Settings = settings,
            InputData = resolvedInputs,
            CancellationToken = cancellationToken,
            ExecutionContext = data.ExecutionContext,
            Connection = connection,
            Action = _action,
            ExpressionData = expressionData,
        };

        // Create and persist step run.
        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = data.RunId,
            StepId = _stepConfig.Id,
            ActionAlias = _action.Alias,
            Status = StepRunStatus.Running,
            StartedUtc = DateTime.UtcNow,
        };

        await _runRepository.SaveStepRunAsync(stepRun, cancellationToken);

        // Execute through the middleware pipeline.
        var result = await _pipeline.ExecuteAsync(_action, actionContext, cancellationToken);

        // Handle WaitingForInput: suspend the workflow until external event.
        if (result.Status == ActionResultStatus.WaitingForInput)
        {
            stepRun.Status = StepRunStatus.WaitingForInput;
            StoreOutputData(result.OutputData, stepRun, data);
            await _runRepository.SaveStepRunAsync(stepRun, cancellationToken);

            _logger.LogInformation(
                "Step {StepId} is waiting for input (event: {EventName}/{EventKey})",
                _stepConfig.Id, result.WaitEventName, result.WaitEventKey);

            return ExecutionResult.WaitForEvent(
                result.WaitEventName!,
                result.WaitEventKey!,
                DateTime.UtcNow);
        }

        // Update step run with result.
        stepRun.CompletedUtc = DateTime.UtcNow;
        stepRun.Duration = stepRun.CompletedUtc - stepRun.StartedUtc;

        switch (result.Status)
        {
            case ActionResultStatus.Success:
                stepRun.Status = StepRunStatus.Completed;
                StoreOutputData(result.OutputData, stepRun, data);
                _metrics.StepExecuted(_action.Alias);
                break;

            case ActionResultStatus.Failed:
                stepRun.Status = StepRunStatus.Failed;
                stepRun.Error = result.Exception?.Message;
                stepRun.ErrorCategory = result.ErrorCategory;
                _metrics.StepFailed(_action.Alias);
                break;

            case ActionResultStatus.Skipped:
                stepRun.Status = StepRunStatus.Skipped;
                break;
        }

        if (stepRun.Duration.HasValue)
        {
            _metrics.RecordStepDuration(stepRun.Duration.Value.TotalMilliseconds, _action.Alias);
        }

        await _runRepository.SaveStepRunAsync(stepRun, cancellationToken);

        // If the step failed and error behavior is Terminate, throw to abort the workflow.
        if (result.Status == ActionResultStatus.Failed && _stepConfig.ErrorBehavior == StepErrorBehavior.Terminate)
        {
            _logger.LogError("Step {StepId} failed with Terminate behavior, aborting workflow", _stepConfig.Id);
            throw result.Exception ?? new InvalidOperationException($"Step '{_stepConfig.Name}' failed.");
        }

        // If the action returned a named outcome, route via WorkflowCore's outcome matching.
        if (result.Status == ActionResultStatus.Success && result.Outcome is not null)
        {
            return ExecutionResult.Outcome(result.Outcome);
        }

        return ExecutionResult.Next();
    }

    private async Task<ExecutionResult> HandleResumeAsync(
        IStepExecutionContext context,
        AutomationWorkflowData data,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Step {StepId} resumed with event data", _stepConfig.Id);

        // Deserialize the decision from the event data.
        ApprovalDecision? decision = null;
        if (context.Item is JsonElement jsonElement)
        {
            decision = JsonSerializer.Deserialize<ApprovalDecision>(
                jsonElement.GetRawText(), Dispatch.JsonOptions.Default);
        }
        else if (context.Item is ApprovalDecision directDecision)
        {
            decision = directDecision;
        }

        // Find the existing step run for this step.
        var run = await _runRepository.GetAsync(data.RunId, cancellationToken);
        var stepRun = run?.StepRuns.FirstOrDefault(sr => sr.StepId == _stepConfig.Id && sr.Status == StepRunStatus.WaitingForInput);

        if (stepRun is null)
        {
            _logger.LogWarning("No WaitingForInput step run found for step {StepId} in run {RunId}", _stepConfig.Id, data.RunId);
            return ExecutionResult.Next();
        }

        stepRun.CompletedUtc = DateTime.UtcNow;
        stepRun.Duration = stepRun.CompletedUtc - stepRun.StartedUtc;

        // Store the decision as step output.
        if (decision is not null)
        {
            var outputData = new
            {
                decision.Outcome,
                decision.Comment,
                decision.ApprovedByUserKey,
                decision.DecisionUtc,
            };
            StoreOutputData(outputData, stepRun, data);
        }

        if (stepRun.Duration.HasValue)
        {
            _metrics.RecordStepDuration(stepRun.Duration.Value.TotalMilliseconds, _action.Alias);
        }

        if (decision?.Outcome == ApprovalOutcome.Approved)
        {
            stepRun.Status = StepRunStatus.Completed;
            await _runRepository.SaveStepRunAsync(stepRun, cancellationToken);
            _metrics.StepExecuted(_action.Alias);
            return ExecutionResult.Next();
        }

        // Rejected or no decision — fail the step.
        stepRun.Status = StepRunStatus.Failed;
        _metrics.StepFailed(_action.Alias);
        stepRun.Error = decision is not null
            ? $"Approval rejected: {decision.Comment ?? "No reason provided"}"
            : "Approval step resumed without a valid decision";
        stepRun.ErrorCategory = StepRunErrorCategory.Cancelled;
        await _runRepository.SaveStepRunAsync(stepRun, cancellationToken);

        if (_stepConfig.ErrorBehavior == StepErrorBehavior.Terminate)
        {
            throw new InvalidOperationException(stepRun.Error);
        }

        return ExecutionResult.Next();
    }

    private void StoreOutputData(object? outputData, StepRun stepRun, AutomationWorkflowData data)
    {
        if (outputData is null)
        {
            return;
        }

        var outputJson = JsonSerializer.Serialize(outputData, Dispatch.JsonOptions.Default);
        stepRun.OutputData = outputJson;
        var outputDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(
            outputJson, Dispatch.JsonOptions.Default) ?? [];
        data.StepOutputs[_stepConfig.Id] = outputDict;
    }

    private static Dictionary<string, object?> BuildExpressionData(AutomationWorkflowData data)
    {
        var expressionData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["trigger"] = data.TriggerOutput,
        };

        // Add step outputs keyed by step ID.
        foreach (var (stepId, outputs) in data.StepOutputs)
        {
            expressionData[$"steps.{stepId}"] = outputs;
        }

        return expressionData;
    }

    private Dictionary<string, object?> ResolveInputMappings(
        Dictionary<string, string> inputMappings,
        Dictionary<string, object?> expressionData)
    {
        var resolved = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, expression) in inputMappings)
        {
            resolved[key] = _expressionEvaluator.Evaluate(expression, expressionData);
        }

        return resolved;
    }
}
