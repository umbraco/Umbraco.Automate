using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// WorkflowCore step body for While container control flow.
/// Evaluates conditions before each iteration, branches children while true.
/// Includes a max iteration safety guard.
/// </summary>
internal sealed class WhileContainerStepBody : StepBody
{
    private readonly WhileControlFlowSettings _settings;
    private readonly ConditionEvaluator _conditionEvaluator;

    public WhileContainerStepBody(
        WhileControlFlowSettings settings,
        ConditionEvaluator conditionEvaluator)
    {
        _settings = settings;
        _conditionEvaluator = conditionEvaluator;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var bindingData = BindingDataBuilder.Build(data);

        // Track iteration count.
        var iteration = 0;
        if (context.PersistenceData is ControlFlowPersistenceData persistence && persistence.Metadata is not null)
        {
            iteration = int.Parse(persistence.Metadata);
        }

        // Safety guard.
        if (iteration >= _settings.MaxIterations)
        {
            return ExecutionResult.Next();
        }

        // Evaluate conditions.
        var conditionsTrue = _conditionEvaluator.Evaluate(_settings.Conditions, bindingData);
        if (!conditionsTrue)
        {
            return ExecutionResult.Next();
        }

        // Branch children for this iteration.
        var nextIteration = iteration + 1;
        return ExecutionResult.Branch(
            [new ForEachIterationContext(null, iteration)],
            new ControlFlowPersistenceData(nameof(WhileContainerStepBody)) { Metadata = nextIteration.ToString() });
    }
}
