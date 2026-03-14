using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
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
    private readonly SwitchControlFlowSettings _settings;
    private readonly ConditionEvaluator _conditionEvaluator;

    public SwitchStepBody(SwitchControlFlowSettings settings, ConditionEvaluator conditionEvaluator)
    {
        _settings = settings;
        _conditionEvaluator = conditionEvaluator;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var bindingData = BindingDataBuilder.Build(data);

        foreach (var switchCase in _settings.Cases)
        {
            if (_conditionEvaluator.Evaluate(switchCase.Conditions, bindingData))
            {
                return ExecutionResult.Outcome(switchCase.Name);
            }
        }

        return ExecutionResult.Outcome("default");
    }
}
