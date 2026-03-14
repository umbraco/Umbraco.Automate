using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// Lightweight WorkflowCore step body for Switch control flow.
/// Evaluates cases in order and returns the first matching case name as the outcome,
/// or "default" if no case matches.
/// Does not go through the action middleware pipeline.
/// </summary>
internal sealed class SwitchStepBody : StepBody
{
    private readonly StepConfiguration _stepConfig;
    private readonly SwitchControlFlowSettings _settings;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IAutomationRunRepository _runRepository;

    public SwitchStepBody(
        StepConfiguration stepConfig,
        SwitchControlFlowSettings settings,
        ConditionEvaluator conditionEvaluator,
        IAutomationRunRepository runRepository)
    {
        _stepConfig = stepConfig;
        _settings = settings;
        _conditionEvaluator = conditionEvaluator;
        _runRepository = runRepository;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var bindingData = BindingDataBuilder.Build(data);

        string outcome = "default";
        foreach (var switchCase in _settings.Cases)
        {
            if (_conditionEvaluator.Evaluate(switchCase.Conditions, bindingData))
            {
                outcome = switchCase.Name;
                break;
            }
        }

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
