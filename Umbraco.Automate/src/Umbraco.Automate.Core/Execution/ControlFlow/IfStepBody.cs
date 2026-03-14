using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
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
    private readonly IfControlFlowSettings _settings;
    private readonly ConditionEvaluator _conditionEvaluator;

    public IfStepBody(IfControlFlowSettings settings, ConditionEvaluator conditionEvaluator)
    {
        _settings = settings;
        _conditionEvaluator = conditionEvaluator;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var bindingData = BindingDataBuilder.Build(data);
        var result = _conditionEvaluator.Evaluate(_settings.Conditions, bindingData);
        return ExecutionResult.Outcome(result ? "true" : "false");
    }
}
