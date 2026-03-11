using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
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
    private readonly IAutomationRunRepository _runRepository;
    private readonly ILogger<ActionStepBody> _logger;

    public ActionStepBody(
        StepConfiguration stepConfig,
        IAction action,
        ActionMiddlewarePipeline pipeline,
        ExpressionEvaluator expressionEvaluator,
        IAutomationRunRepository runRepository,
        ILogger<ActionStepBody> logger)
    {
        _stepConfig = stepConfig;
        _action = action;
        _pipeline = pipeline;
        _expressionEvaluator = expressionEvaluator;
        _runRepository = runRepository;
        _logger = logger;
    }

    public override async Task<ExecutionResult> RunAsync(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var cancellationToken = context.CancellationToken;

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

        // Update step run with result.
        stepRun.CompletedUtc = DateTime.UtcNow;
        stepRun.Duration = stepRun.CompletedUtc - stepRun.StartedUtc;

        switch (result.Status)
        {
            case ActionResultStatus.Success:
                stepRun.Status = StepRunStatus.Completed;

                // Store output for downstream steps.
                if (result.OutputData is not null)
                {
                    var outputJson = JsonSerializer.Serialize(result.OutputData, Dispatch.JsonOptions.Default);
                    var outputDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(
                        outputJson, Dispatch.JsonOptions.Default) ?? [];
                    data.StepOutputs[_stepConfig.Id] = outputDict;
                }

                break;

            case ActionResultStatus.Failed:
                stepRun.Status = StepRunStatus.Failed;
                stepRun.Error = result.Exception?.Message;
                stepRun.ErrorCategory = result.ErrorCategory;
                break;

            case ActionResultStatus.Skipped:
                stepRun.Status = StepRunStatus.Skipped;
                break;
        }

        await _runRepository.SaveStepRunAsync(stepRun, cancellationToken);

        // If the step failed and error behavior is Terminate, throw to abort the workflow.
        if (result.Status == ActionResultStatus.Failed && _stepConfig.ErrorBehavior == StepErrorBehavior.Terminate)
        {
            _logger.LogError("Step {StepId} failed with Terminate behavior, aborting workflow", _stepConfig.Id);
            throw result.Exception ?? new InvalidOperationException($"Step '{_stepConfig.Name}' failed.");
        }

        return ExecutionResult.Next();
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
