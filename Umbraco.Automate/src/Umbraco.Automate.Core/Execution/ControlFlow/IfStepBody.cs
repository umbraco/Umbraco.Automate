using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// Lightweight WorkflowCore step body for If control flow.
/// Evaluates a <see cref="ConditionSet"/> and returns an outcome of "true" or "false".
/// Does not go through the action middleware pipeline.
/// </summary>
internal sealed class IfStepBody : StepBody
{
    private readonly StepConfiguration _stepConfig;
    private readonly IfControlFlowSettings _settings;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly StepOutputHydrationCache _hydrationCache;
    private readonly IAutomationRunRepository _runRepository;

    public IfStepBody(
        StepConfiguration stepConfig,
        IfControlFlowSettings settings,
        ConditionEvaluator conditionEvaluator,
        StepOutputHydrationCache hydrationCache,
        IAutomationRunRepository runRepository)
    {
        _stepConfig = stepConfig;
        _settings = settings;
        _conditionEvaluator = conditionEvaluator;
        _hydrationCache = hydrationCache;
        _runRepository = runRepository;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var bindingData = BindingDataBuilder.Build(data, hydrationCache: _hydrationCache, cancellationToken: context.CancellationToken);
        var result = _conditionEvaluator.Evaluate(_settings.Conditions, bindingData);
        var outcome = result ? "true" : "false";

        // Track step execution.
        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = data.RunId,
            StepId = _stepConfig.Id,
            ActionAlias = _stepConfig.ActionAlias,
            Status = StepRunStatus.Completed,
            StartedUtc = DateTime.UtcNow,
            CompletedUtc = DateTime.UtcNow,
            BranchOutcome = outcome,
        };
        _runRepository.SaveStepRunAsync(stepRun, context.CancellationToken).GetAwaiter().GetResult();

        return ExecutionResult.Outcome(outcome);
    }
}
