using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// Covers on-demand hydration of offloaded step outputs: binding evaluation that touches a
/// marker fetches the full output from the StepRun table (lazily, memoised, null-tolerant),
/// while inline outputs and unrelated bindings never pay a read.
/// </summary>
public class StepOutputHydrationTests
{
    private static readonly Guid RunId = Guid.NewGuid();

    private readonly Mock<IAutomationRunRepository> _runRepository = new();
    private readonly BindingEvaluator _evaluator = new(new BindingFilterCollection(Array.Empty<IBindingFilter>));

    private StepOutputHydrationCache CreateCache() => new(_runRepository.Object);

    private void SetupStepRunOutput(Guid stepRunId, string? outputJson)
        => _runRepository
            .Setup(r => r.GetStepRunOutputAsync(stepRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(outputJson);

    private static AutomationWorkflowData CreateData(Guid stepId, Dictionary<string, object?> outputs, string alias = "offloaded")
        => new()
        {
            RunId = RunId,
            StepOutputs = new Dictionary<Guid, Dictionary<string, object?>> { [stepId] = outputs },
            StepAliases = new Dictionary<Guid, string> { [stepId] = alias },
        };

    [Fact]
    public void Build_MarkerOutput_HydratesFieldFromStepRunTable()
    {
        var stepId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        SetupStepRunOutput(stepRunId, """{"message":"the-large-value","statusCode":200}""");
        var data = CreateData(stepId, StepOutputReference.CreateMarker(stepRunId));

        var bindingData = BindingDataBuilder.Build(data, hydrationCache: CreateCache());

        _evaluator.Evaluate("${ steps.offloaded.message }", bindingData).ShouldBe("the-large-value");
        _evaluator.Evaluate($"${{ steps.{stepId}.statusCode }}", bindingData).ShouldBe("200");
    }

    [Fact]
    public void Build_MarkerOutput_NotFetchedWhenNothingBindsIntoIt()
    {
        var offloadedStepId = Guid.NewGuid();
        var inlineStepId = Guid.NewGuid();
        var data = new AutomationWorkflowData
        {
            RunId = RunId,
            StepOutputs = new Dictionary<Guid, Dictionary<string, object?>>
            {
                [offloadedStepId] = StepOutputReference.CreateMarker(Guid.NewGuid()),
                [inlineStepId] = new(StringComparer.OrdinalIgnoreCase) { ["message"] = "small" },
            },
            StepAliases = new Dictionary<Guid, string>
            {
                [offloadedStepId] = "offloaded",
                [inlineStepId] = "inline",
            },
        };

        var bindingData = BindingDataBuilder.Build(data, hydrationCache: CreateCache());
        _evaluator.Evaluate("${ steps.inline.message }", bindingData).ShouldBe("small");

        _runRepository.Verify(
            r => r.GetStepRunOutputAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Build_RepeatedBindsToSameOffloadedOutput_FetchesOnce()
    {
        var stepId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        SetupStepRunOutput(stepRunId, """{"message":"memoised"}""");
        var data = CreateData(stepId, StepOutputReference.CreateMarker(stepRunId));
        var cache = CreateCache();

        // Two binds through one Build, then a fresh Build (a later step's evaluation pass).
        var bindingData = BindingDataBuilder.Build(data, hydrationCache: cache);
        _evaluator.Evaluate("${ steps.offloaded.message }", bindingData).ShouldBe("memoised");
        _evaluator.Evaluate("${ steps.offloaded.message }", bindingData).ShouldBe("memoised");
        var laterBindingData = BindingDataBuilder.Build(data, hydrationCache: cache);
        _evaluator.Evaluate("${ steps.offloaded.message }", laterBindingData).ShouldBe("memoised");

        _runRepository.Verify(
            r => r.GetStepRunOutputAsync(stepRunId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Build_WholeOutputBind_StringifiesHydratedOutput()
    {
        // ${steps.x} with no field — Stringify must JSON-serialize the hydrated dictionary,
        // not the marker or the lazy stand-in's type name.
        var stepId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        SetupStepRunOutput(stepRunId, """{"message":"whole-bind"}""");
        var data = CreateData(stepId, StepOutputReference.CreateMarker(stepRunId));

        var bindingData = BindingDataBuilder.Build(data, hydrationCache: CreateCache());

        var result = _evaluator.Evaluate("${ steps.offloaded }", bindingData);
        result.ShouldContain("whole-bind");
        result.ShouldNotContain(StepOutputReference.MarkerKey);
    }

    [Fact]
    public void Build_StepRunRowMissing_ResolvesAsMissingPathWithoutThrowing()
    {
        var stepId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        SetupStepRunOutput(stepRunId, null);
        var data = CreateData(stepId, StepOutputReference.CreateMarker(stepRunId));

        var bindingData = BindingDataBuilder.Build(data, hydrationCache: CreateCache());

        _evaluator.EvaluateRaw("${ steps.offloaded.message }", bindingData).ShouldBeNull();
        _evaluator.Evaluate("${ steps.offloaded.message }", bindingData).ShouldBe(string.Empty);
    }

    [Fact]
    public void Build_PreviousBinding_HydratesMarker()
    {
        var stepId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        SetupStepRunOutput(stepRunId, """{"message":"from-previous"}""");
        var data = CreateData(stepId, StepOutputReference.CreateMarker(stepRunId));
        data.LastCompletedStepId = stepId;

        var bindingData = BindingDataBuilder.Build(data, hydrationCache: CreateCache());

        _evaluator.Evaluate("${ previous.message }", bindingData).ShouldBe("from-previous");
    }

    [Fact]
    public void Build_IterationScopedMarker_HydratesLikeGlobalOutputs()
    {
        var containerId = Guid.NewGuid();
        var bodyStepId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        SetupStepRunOutput(stepRunId, """{"message":"iteration-value"}""");

        var iteration = new ForEachIterationContext(null, 0, containerId, null);
        var data = new AutomationWorkflowData
        {
            RunId = RunId,
            IterationStepOutputs = new Dictionary<string, Dictionary<Guid, Dictionary<string, object?>>>
            {
                [iteration.ScopePath] = new()
                {
                    [bodyStepId] = StepOutputReference.CreateMarker(stepRunId),
                },
            },
            StepAliases = new Dictionary<Guid, string> { [bodyStepId] = "bodyStep" },
        };

        var bindingData = BindingDataBuilder.Build(data, iteration, hydrationCache: CreateCache());

        _evaluator.Evaluate("${ steps.bodyStep.message }", bindingData).ShouldBe("iteration-value");
    }

    [Fact]
    public void Build_InlineOutputs_ResolveUnchangedWithHydrationCachePresent()
    {
        // Backward compatibility: pre-offload blobs contain plain dictionaries and must
        // resolve exactly as before, with zero hydration reads.
        var stepId = Guid.NewGuid();
        var data = CreateData(
            stepId,
            new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["message"] = "plain-inline" });

        var bindingData = BindingDataBuilder.Build(data, hydrationCache: CreateCache());

        _evaluator.Evaluate("${ steps.offloaded.message }", bindingData).ShouldBe("plain-inline");
        _runRepository.Verify(
            r => r.GetStepRunOutputAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void Build_WithoutHydrationCache_LeavesMarkerUnresolvedWithoutThrowing()
    {
        // Call sites that cannot hydrate (no cache passed) degrade to missing-path
        // semantics rather than crashing.
        var stepId = Guid.NewGuid();
        var data = CreateData(stepId, StepOutputReference.CreateMarker(Guid.NewGuid()));

        var bindingData = BindingDataBuilder.Build(data);

        _evaluator.EvaluateRaw("${ steps.offloaded.message }", bindingData).ShouldBeNull();
    }

    [Fact]
    public void Cache_MissingRow_IsMemoisedToo()
    {
        var stepRunId = Guid.NewGuid();
        SetupStepRunOutput(stepRunId, null);
        var cache = CreateCache();

        cache.GetOutput(RunId, stepRunId).ShouldBeEmpty();
        cache.GetOutput(RunId, stepRunId).ShouldBeEmpty();

        _runRepository.Verify(
            r => r.GetStepRunOutputAsync(stepRunId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Cache_EvictRun_RemovesThatRunsEntriesOnly()
    {
        var stepRunA = Guid.NewGuid();
        var stepRunB = Guid.NewGuid();
        var otherRunId = Guid.NewGuid();
        SetupStepRunOutput(stepRunA, """{"a":1}""");
        SetupStepRunOutput(stepRunB, """{"b":2}""");
        var cache = CreateCache();

        cache.GetOutput(RunId, stepRunA);
        cache.GetOutput(otherRunId, stepRunB);

        cache.EvictRun(RunId);

        cache.GetOutput(RunId, stepRunA);
        cache.GetOutput(otherRunId, stepRunB);

        _runRepository.Verify(r => r.GetStepRunOutputAsync(stepRunA, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _runRepository.Verify(r => r.GetStepRunOutputAsync(stepRunB, It.IsAny<CancellationToken>()), Times.Once);
    }
}
