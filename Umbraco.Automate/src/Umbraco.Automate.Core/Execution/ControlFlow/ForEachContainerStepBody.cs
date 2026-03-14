using System.Collections;
using System.Text.Json;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// WorkflowCore step body for ForEach container control flow.
/// Evaluates a collection binding expression and branches child steps for each item.
/// </summary>
internal sealed class ForEachContainerStepBody : StepBody
{
    private readonly ForEachControlFlowSettings _settings;
    private readonly BindingEvaluator _bindingEvaluator;

    public ForEachContainerStepBody(
        ForEachControlFlowSettings settings,
        BindingEvaluator bindingEvaluator)
    {
        _settings = settings;
        _bindingEvaluator = bindingEvaluator;
    }

    public override ExecutionResult Run(IStepExecutionContext context)
    {
        var data = (AutomationWorkflowData)context.Workflow.Data;
        var bindingData = BindingDataBuilder.Build(data);

        // Evaluate the collection expression.
        var collectionValue = _bindingEvaluator.Evaluate(_settings.Collection, bindingData);

        var items = ResolveCollection(collectionValue);
        if (items.Count == 0)
        {
            return ExecutionResult.Next();
        }

        // Branch for each item.
        if (_settings.RunParallel)
        {
            // Branch all items simultaneously.
            var branches = items.Select((item, index) => new ForEachIterationContext(item, index)).Cast<object>().ToList();
            return ExecutionResult.Branch(branches, new ControlFlowPersistenceData(nameof(ForEachContainerStepBody)));
        }

        // Sequential: check if we're resuming from a previous iteration.
        if (context.PersistenceData is ControlFlowPersistenceData persistence && persistence.Metadata is not null)
        {
            var currentIndex = int.Parse(persistence.Metadata);
            var nextIndex = currentIndex + 1;

            if (nextIndex >= items.Count)
            {
                return ExecutionResult.Next();
            }

            var nextItem = new ForEachIterationContext(items[nextIndex], nextIndex);
            return ExecutionResult.Branch(
                [nextItem],
                new ControlFlowPersistenceData(nameof(ForEachContainerStepBody)) { Metadata = nextIndex.ToString() });
        }

        // First iteration.
        var firstItem = new ForEachIterationContext(items[0], 0);
        return ExecutionResult.Branch(
            [firstItem],
            new ControlFlowPersistenceData(nameof(ForEachContainerStepBody)) { Metadata = "0" });
    }

    private static List<object?> ResolveCollection(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        // Try to parse as JSON array.
        try
        {
            using var doc = JsonDocument.Parse(value);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                return doc.RootElement.EnumerateArray()
                    .Select(e => (object?)e.ToString())
                    .ToList();
            }
        }
        catch (JsonException)
        {
            // Not JSON — treat as comma-separated.
        }

        // Fall back to comma-separated values.
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Cast<object?>()
            .ToList();
    }
}
