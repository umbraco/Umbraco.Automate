using System.Text.Json;
using Umbraco.Automate.Core.Execution.ControlFlow;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Builds the binding data dictionary from workflow data for use in condition evaluation
/// and binding resolution. Shared by <see cref="ActionStepBody"/> and control flow step bodies.
/// </summary>
internal static class BindingDataBuilder
{
    /// <summary>
    /// Builds a binding data dictionary containing trigger output and accumulated step outputs.
    /// </summary>
    /// <param name="data">The workflow data carrying per-run state.</param>
    /// <param name="iterationContext">Optional ForEach iteration context for child steps inside a loop.</param>
    /// <param name="collectionCache">Optional cache used to resolve <c>loop.item</c> for index-only iteration contexts.</param>
    /// <param name="hydrationCache">Optional cache used to lazily hydrate offloaded step outputs
    /// (see <see cref="StepOutputReference"/>). Without it markers resolve as missing paths.</param>
    /// <param name="cancellationToken">Propagated to the lazy hydration read so it can be
    /// cancelled if the step or workflow is cancelled before a binding actually reads an
    /// offloaded output. Defaults to <see cref="CancellationToken.None"/> for call sites
    /// (e.g. WorkflowCore outcome-value delegates) that have no token available.</param>
    /// <returns>A dictionary suitable for binding evaluation.</returns>
    public static Dictionary<string, object?> Build(
        AutomationWorkflowData data,
        ForEachIterationContext? iterationContext = null,
        ForEachCollectionCache? collectionCache = null,
        StepOutputHydrationCache? hydrationCache = null,
        CancellationToken cancellationToken = default)
    {
        var stepsDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Layer 1 — global step outputs (steps outside any iteration scope).
        foreach (var (stepId, outputs) in data.StepOutputs)
        {
            AddStepEntry(stepsDict, data, stepId, outputs, hydrationCache, cancellationToken);
        }

        // Layer 2 — iteration-scoped outputs from this iteration and its ancestors.
        // Walking outermost → innermost lets the deepest scope win on conflicts, which
        // matches lexical scoping in nested ForEach loops.
        foreach (var ancestor in EnumerateAncestorsOutermostFirst(iterationContext))
        {
            if (!data.IterationStepOutputs.TryGetValue(ancestor.ScopePath, out var iterOutputs))
            {
                continue;
            }

            foreach (var (stepId, outputs) in iterOutputs)
            {
                AddStepEntry(stepsDict, data, stepId, outputs, hydrationCache, cancellationToken);
            }
        }

        var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["trigger"] = data.TriggerOutput,
            ["steps"] = stepsDict,
        };

        // The "previous" key prefers an iteration-scoped last-completed step (so the first
        // body step in each iteration reads the iteration's own preceding step) and falls
        // back to the run-global pointer for steps outside any loop.
        var previousStepId = ResolvePreviousStepId(data, iterationContext);
        if (previousStepId.HasValue && stepsDict.TryGetValue(previousStepId.Value.ToString(), out var previousOutputs))
        {
            bindingData["previous"] = previousOutputs;
        }
        else if (previousStepId.HasValue && data.StepAliases.TryGetValue(previousStepId.Value, out var previousAlias)
            && stepsDict.TryGetValue(previousAlias, out var previousByAlias))
        {
            bindingData["previous"] = previousByAlias;
        }

        if (iterationContext is not null)
        {
            // Contexts persisted by older versions embed the item — prefer it so in-flight
            // runs keep resolving. New contexts carry only the index; look the item up in
            // the per-run materialised collection.
            var item = iterationContext.Item ?? collectionCache?.ResolveItem(data, iterationContext, cancellationToken);
            bindingData["loop"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["item"] = ResolveIterationItem(item),
                ["index"] = iterationContext.Index,
            };
        }

        return bindingData;
    }

    private static void AddStepEntry(
        Dictionary<string, object?> stepsDict,
        AutomationWorkflowData data,
        Guid stepId,
        Dictionary<string, object?> outputs,
        StepOutputHydrationCache? hydrationCache,
        CancellationToken cancellationToken)
    {
        // Offloaded output — substitute a lazy stand-in that hydrates from the StepRun table
        // on first bind. The marker itself stays in the workflow data; the stand-in only ever
        // lives in this per-call binding dictionary, so it is never persisted.
        object? entry = outputs;
        if (hydrationCache is not null && StepOutputReference.TryGetStepRunId(outputs, out var stepRunId))
        {
            entry = new OffloadedStepOutput(hydrationCache, data.RunId, stepRunId, cancellationToken);
        }

        stepsDict[stepId.ToString()] = entry;

        // Also register by alias so both ${steps.GUID.field} and ${steps.alias.field} work.
        if (data.StepAliases.TryGetValue(stepId, out var alias))
        {
            stepsDict[alias] = entry;
        }
    }

    private static IEnumerable<ForEachIterationContext> EnumerateAncestorsOutermostFirst(ForEachIterationContext? iterationContext)
    {
        if (iterationContext is null)
        {
            yield break;
        }

        var stack = new Stack<ForEachIterationContext>();
        var current = iterationContext;
        while (current is not null)
        {
            stack.Push(current);
            current = current.Parent;
        }

        while (stack.Count > 0)
        {
            yield return stack.Pop();
        }
    }

    private static Guid? ResolvePreviousStepId(AutomationWorkflowData data, ForEachIterationContext? iterationContext)
    {
        // Innermost iteration scope wins; fall back to outer scopes and finally the run-global pointer.
        var current = iterationContext;
        while (current is not null)
        {
            if (data.IterationLastCompletedStepId.TryGetValue(current.ScopePath, out var iterPrev) && iterPrev.HasValue)
            {
                return iterPrev;
            }
            current = current.Parent;
        }

        return data.LastCompletedStepId;
    }

    /// <summary>
    /// Resolves a ForEach iteration item to a binding-compatible value.
    /// JSON strings are parsed into structured types (dictionaries, lists, or primitives)
    /// for nested path traversal. This remains necessary as a defence against the
    /// Newtonsoft.Json round-trip in WorkflowCore persistence, which can turn structured
    /// data back into JSON strings after workflow suspension and resumption.
    /// </summary>
    private static object? ResolveIterationItem(object? item)
    {
        if (item is not string jsonString || string.IsNullOrWhiteSpace(jsonString))
        {
            return item;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            return Dispatch.JsonOptions.UnwrapJsonElement(doc.RootElement);
        }
        catch (JsonException)
        {
            // Not JSON — return as-is (simple string value).
        }

        return item;
    }
}
