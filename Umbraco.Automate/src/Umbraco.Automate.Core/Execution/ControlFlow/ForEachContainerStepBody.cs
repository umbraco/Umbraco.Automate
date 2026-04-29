using System.Text.Json;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Runs;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution.ControlFlow;

/// <summary>
/// WorkflowCore step body for ForEach container control flow.
/// Evaluates a collection binding expression and branches child steps for each item.
/// </summary>
internal sealed class ForEachContainerStepBody : StepBody
{
    private readonly StepConfiguration _stepConfig;
    private readonly ForEachControlFlowSettings _settings;
    private readonly BindingEvaluator _bindingEvaluator;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly IAutomationRunRepository _runRepository;
    private readonly IReadOnlyList<ContainerBranchEdge> _branchEdges;

    public ForEachContainerStepBody(
        StepConfiguration stepConfig,
        ForEachControlFlowSettings settings,
        BindingEvaluator bindingEvaluator,
        ConditionEvaluator conditionEvaluator,
        IAutomationRunRepository runRepository,
        IReadOnlyList<ContainerBranchEdge> branchEdges)
    {
        _stepConfig = stepConfig;
        _settings = settings;
        _bindingEvaluator = bindingEvaluator;
        _conditionEvaluator = conditionEvaluator;
        _runRepository = runRepository;
        _branchEdges = branchEdges;
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
            TrackStepRun(data, context.CancellationToken, iterationIndex: null, iterationTotal: 0);
            return ExecutionResult.Next();
        }

        // Branch for each item.
        if (_settings.RunParallel)
        {
            // Filter items whose outgoing-edge filters all fail. The branch entries themselves
            // (step.Children) cannot be filtered per-item — WorkflowCore cross-products
            // BranchValues × Children — so the filter gates the iteration as a whole. For the
            // common single-entry case this is the natural "skip this item" behaviour.
            var passing = items
                .Select((item, index) => (Item: item, Index: index))
                .Where(t => AnyEdgePassesForItem(data, t.Item, t.Index))
                .ToList();

            TrackStepRun(data, context.CancellationToken, iterationIndex: null, iterationTotal: items.Count);

            if (passing.Count == 0)
            {
                return ExecutionResult.Next();
            }

            var branches = passing.Select(t => (object)new ForEachIterationContext(t.Item, t.Index)).ToList();
            return ExecutionResult.Branch(branches, new ControlFlowPersistenceData(nameof(ForEachContainerStepBody)));
        }

        // Sequential: walk forward from the next index, skipping items whose filters fail.
        var startIndex = 0;
        var firstIteration = true;
        if (context.PersistenceData is ControlFlowPersistenceData persistence &&
            persistence.Metadata is not null &&
            int.TryParse(persistence.Metadata, out var lastIndex))
        {
            startIndex = lastIndex + 1;
            firstIteration = false;
        }

        var nextIndex = FindNextPassingIndex(data, items, startIndex);

        if (nextIndex < 0)
        {
            if (firstIteration)
            {
                // Nothing in the collection passes the filter — record total but don't branch.
                TrackStepRun(data, context.CancellationToken, iterationIndex: null, iterationTotal: items.Count);
            }

            return ExecutionResult.Next();
        }

        if (firstIteration)
        {
            TrackStepRun(data, context.CancellationToken, iterationIndex: nextIndex, iterationTotal: items.Count);
        }

        var nextItem = new ForEachIterationContext(items[nextIndex], nextIndex);
        return ExecutionResult.Branch(
            [nextItem],
            new ControlFlowPersistenceData(nameof(ForEachContainerStepBody)) { Metadata = nextIndex.ToString() });
    }

    private int FindNextPassingIndex(AutomationWorkflowData data, IReadOnlyList<object?> items, int from)
    {
        for (var i = from; i < items.Count; i++)
        {
            if (AnyEdgePassesForItem(data, items[i], i))
            {
                return i;
            }
        }
        return -1;
    }

    private bool AnyEdgePassesForItem(AutomationWorkflowData data, object? item, int index)
    {
        if (_branchEdges.Count == 0)
        {
            // No outgoing edges recorded — defer to the legacy "always branch" behaviour.
            // (Container with no outgoing connection won't be branched against anything anyway.)
            return true;
        }

        // If no edge has a filter, all pass trivially.
        var hasAnyFilter = false;
        foreach (var edge in _branchEdges)
        {
            if (edge.Filter is not null)
            {
                hasAnyFilter = true;
                break;
            }
        }
        if (!hasAnyFilter)
        {
            return true;
        }

        var iterationContext = new ForEachIterationContext(item, index);
        var bindingData = BindingDataBuilder.Build(data, iterationContext);

        foreach (var edge in _branchEdges)
        {
            if (edge.Filter is null || _conditionEvaluator.Evaluate(edge.Filter, bindingData))
            {
                return true;
            }
        }

        return false;
    }

    private void TrackStepRun(AutomationWorkflowData data, CancellationToken ct, int? iterationIndex, int iterationTotal)
    {
        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = data.RunId,
            StepId = _stepConfig.Id,
            ActionAlias = _stepConfig.ActionAlias,
            Status = StepRunStatus.Completed,
            StartedUtc = DateTime.UtcNow,
            CompletedUtc = DateTime.UtcNow,
            IterationIndex = iterationIndex,
            IterationTotal = iterationTotal,
        };
        _runRepository.SaveStepRunAsync(stepRun, ct).GetAwaiter().GetResult();
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
                    .Select(e => Dispatch.JsonOptions.UnwrapJsonElement(e))
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
