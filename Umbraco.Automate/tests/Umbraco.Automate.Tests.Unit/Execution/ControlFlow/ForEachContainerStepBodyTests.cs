using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Testing.Builders;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution.ControlFlow;

public class ForEachContainerStepBodyTests
{
    private readonly BindingEvaluator _bindingEvaluator;
    private readonly StepOutputHydrationCache _hydrationCache;
    private readonly ForEachCollectionCache _collectionCache;
    private readonly ConditionEvaluator _conditionEvaluator;
    private readonly Mock<IAutomationRunRepository> _runRepo = new();

    public ForEachContainerStepBodyTests()
    {
        _bindingEvaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        _hydrationCache = new StepOutputHydrationCache(_runRepo.Object);
        _collectionCache = new ForEachCollectionCache(_bindingEvaluator, _hydrationCache);
        _conditionEvaluator = new ConditionEvaluator(_bindingEvaluator);
        _runRepo.Setup(r => r.AddStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StepRun sr, CancellationToken _) => sr);
    }

    [Fact]
    public void Run_EmptyCollection_ReturnsNext()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "" });
        var result = body.Run(CreateContext());

        result.Proceed.ShouldBeTrue();
        result.BranchValues.ShouldBeEmpty();
    }

    [Fact]
    public void Run_CommaSeparatedCollection_Parallel_BranchesAllItems()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = true });
        var result = body.Run(CreateContext());

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(3);
    }

    [Fact]
    public void Run_JsonArrayCollection_Parallel_BranchesAllItems()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            TriggerOutput = new Dictionary<string, object?> { ["items"] = "[\"x\",\"y\"]" },
        };
        var body = CreateBody(new ForEachControlFlowSettings
        {
            Collection = "${trigger.items}",
            RunParallel = true,
        });
        var result = body.Run(CreateContext(data));

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(2);
    }

    [Fact]
    public void Run_Sequential_FirstIteration_BranchesSingleItem()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = false });
        var result = body.Run(CreateContext());

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(1);

        var iteration = (ForEachIterationContext)result.BranchValues[0];
        iteration.Index.ShouldBe(0);
    }

    [Fact]
    public void Run_Sequential_ResumeIteration_AdvancesIndex()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = false });
        var persistence = new IteratorPersistenceData { ChildrenActive = true, Index = 1 };
        var result = body.Run(CreateContext(persistenceData: persistence));

        result.BranchValues.ShouldNotBeNull();
        var iteration = (ForEachIterationContext)result.BranchValues[0];
        iteration.Index.ShouldBe(2);
    }

    [Fact]
    public void Run_Sequential_LastIteration_ReturnsNext()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b", RunParallel = false });
        var persistence = new IteratorPersistenceData { ChildrenActive = true, Index = 1 };
        var result = body.Run(CreateContext(persistenceData: persistence));

        result.BranchValues.ShouldBeEmpty();
        result.Proceed.ShouldBeTrue();
    }

    [Fact]
    public void Run_Parallel_ItemsFailingEdgeFilter_AreSkipped()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            TriggerOutput = new Dictionary<string, object?>
            {
                ["items"] = "[\"\",\"x\",\"\",\"y\"]",
            },
        };

        var filter = SingleFilter("${ loop.item }", ConditionOperator.IsNotEmpty, string.Empty);

        var body = CreateBody(
            new ForEachControlFlowSettings { Collection = "${trigger.items}", RunParallel = true },
            branchEdges: [new ContainerBranchEdge(Guid.NewGuid(), filter)]);

        var result = body.Run(CreateContext(data));

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(2);
        ((ForEachIterationContext)result.BranchValues[0]).Index.ShouldBe(1);
        ((ForEachIterationContext)result.BranchValues[1]).Index.ShouldBe(3);
    }

    [Fact]
    public void Run_Parallel_AllItemsFailFilter_ReturnsNext()
    {
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            TriggerOutput = new Dictionary<string, object?>
            {
                ["items"] = "[\"\",\"\"]",
            },
        };

        var filter = SingleFilter("${ loop.item }", ConditionOperator.IsNotEmpty, string.Empty);

        var body = CreateBody(
            new ForEachControlFlowSettings { Collection = "${trigger.items}", RunParallel = true },
            branchEdges: [new ContainerBranchEdge(Guid.NewGuid(), filter)]);

        var result = body.Run(CreateContext(data));

        result.Proceed.ShouldBeTrue();
        result.BranchValues.ShouldBeEmpty();
    }

    [Fact]
    public void Run_Sequential_FirstPassingItem_IsBranched()
    {
        var filter = SingleFilter("${ loop.item }", ConditionOperator.Equals, "b");

        var body = CreateBody(
            new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = false },
            branchEdges: [new ContainerBranchEdge(Guid.NewGuid(), filter)]);

        var result = body.Run(CreateContext());

        result.BranchValues.ShouldNotBeNull();
        var iteration = (ForEachIterationContext)result.BranchValues[0];
        iteration.Index.ShouldBe(1);

        var persistence = result.PersistenceData as IteratorPersistenceData;
        persistence.ShouldNotBeNull();
        persistence.ChildrenActive.ShouldBeTrue();
        persistence.Index.ShouldBe(1);
    }

    [Fact]
    public void Run_Sequential_NoPassingItems_ReturnsNext()
    {
        var filter = SingleFilter("${ loop.item }", ConditionOperator.Equals, "z");

        var body = CreateBody(
            new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = false },
            branchEdges: [new ContainerBranchEdge(Guid.NewGuid(), filter)]);

        var result = body.Run(CreateContext());

        result.Proceed.ShouldBeTrue();
        result.BranchValues.ShouldBeEmpty();
    }

    [Fact]
    public void Run_Sequential_Resume_SkipsItemsThatFailFilter()
    {
        // OR across two groups: accept item == "a" OR item == "c". After resuming with
        // metadata="0", the body must skip "b" and branch on "c" at index 2.
        var filter = new ConditionSet
        {
            Groups =
            [
                new ConditionGroup { Conditions = [new Condition { LeftOperand = "${ loop.item }", Operator = ConditionOperator.Equals, RightOperand = "a" }] },
                new ConditionGroup { Conditions = [new Condition { LeftOperand = "${ loop.item }", Operator = ConditionOperator.Equals, RightOperand = "c" }] },
            ],
        };

        var body = CreateBody(
            new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = false },
            branchEdges: [new ContainerBranchEdge(Guid.NewGuid(), filter)]);

        var persistence = new IteratorPersistenceData { ChildrenActive = true, Index = 0 };
        var result = body.Run(CreateContext(persistenceData: persistence));

        var iteration = (ForEachIterationContext)result.BranchValues![0];
        iteration.Index.ShouldBe(2);
    }

    [Fact]
    public void Run_FilterUsesItemFieldBinding_SkipsItemsWithEmptyField()
    {
        // Mirrors the user's reported scenario: filter on
        // ${ loop.item.fields[\"Training Organization Name\"] } IsNotEmpty.
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            TriggerOutput = new Dictionary<string, object?>
            {
                ["records"] = "[{\"fields\":{\"Name\":\"\"}},{\"fields\":{\"Name\":\"Acme\"}}]",
            },
        };

        var filter = SingleFilter("${ loop.item.fields.Name }", ConditionOperator.IsNotEmpty, string.Empty);

        var body = CreateBody(
            new ForEachControlFlowSettings { Collection = "${trigger.records}", RunParallel = true },
            branchEdges: [new ContainerBranchEdge(Guid.NewGuid(), filter)]);

        var result = body.Run(CreateContext(data));

        result.BranchValues.ShouldNotBeNull();
        result.BranchValues.Count.ShouldBe(1);
        ((ForEachIterationContext)result.BranchValues[0]).Index.ShouldBe(1);
    }

    [Fact]
    public void Run_Sequential_BranchValue_CarriesNoItem()
    {
        // Branch values are persisted into every body-step pointer by WorkflowCore, so the
        // context must carry only the index — items are resolved at binding time from the
        // per-run collection cache.
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = false });
        var result = body.Run(CreateContext());

        var iteration = (ForEachIterationContext)result.BranchValues![0];
        iteration.Index.ShouldBe(0);
        iteration.Item.ShouldBeNull();
    }

    [Fact]
    public void Run_Parallel_BranchValues_CarryNoItems()
    {
        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b", RunParallel = true });
        var result = body.Run(CreateContext());

        result.BranchValues!.Cast<ForEachIterationContext>()
            .ShouldAllBe(iteration => iteration.Item == null);
    }

    [Fact]
    public void Run_FirstEntry_StashesCollectionExpression()
    {
        // The expression (not the materialised items) is persisted with the workflow data
        // so item resolution can re-materialise the collection after a process restart.
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.forEach").WithName("ForEach");
        var data = CreateData();

        var body = CreateBody(new ForEachControlFlowSettings { Collection = "${trigger.items}" }, stepConfig: stepConfig);
        body.Run(CreateContext(data));

        data.ContainerCollections[stepConfig.Id].ShouldBe("${trigger.items}");
    }

    [Fact]
    public void Run_Sequential_ReEntry_ReusesMaterialisedCollection()
    {
        // The collection is materialised once per run and reused on every sequential
        // re-entry. Mutating the underlying trigger data between iterations must not
        // change the in-flight collection (previously it was re-parsed every iteration).
        var data = CreateData();
        data.TriggerOutput["items"] = "[\"x\",\"y\",\"z\"]";

        var body = CreateBody(new ForEachControlFlowSettings { Collection = "${trigger.items}", RunParallel = false });
        var first = body.Run(CreateContext(data));
        ((ForEachIterationContext)first.BranchValues![0]).Index.ShouldBe(0);

        data.TriggerOutput["items"] = "[]";

        var persistence = new IteratorPersistenceData { ChildrenActive = true, Index = 0 };
        var second = body.Run(CreateContext(data, persistence));

        second.BranchValues.ShouldNotBeEmpty();
        ((ForEachIterationContext)second.BranchValues![0]).Index.ShouldBe(1);
    }

    [Fact]
    public void Run_Sequential_ReEntry_PrunesCompletedIterationScope()
    {
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.forEach").WithName("ForEach");
        var bodyStepId = Guid.NewGuid();
        var completedScope = $"{stepConfig.Id:N}:0";
        var nestedScope = $"{completedScope}/{Guid.NewGuid():N}:2";
        var siblingContainerScope = $"{Guid.NewGuid():N}:0";

        var data = CreateData();
        data.IterationStepOutputs[completedScope] = new() { [bodyStepId] = new() { ["message"] = "iter-0" } };
        data.IterationStepOutputs[nestedScope] = new() { [bodyStepId] = new() { ["message"] = "nested" } };
        data.IterationStepOutputs[siblingContainerScope] = new() { [bodyStepId] = new() { ["message"] = "other" } };
        data.IterationLastCompletedStepId[completedScope] = bodyStepId;
        data.IterationLastCompletedStepId[nestedScope] = bodyStepId;

        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b, c" }, stepConfig: stepConfig);
        var persistence = new IteratorPersistenceData { ChildrenActive = true, Index = 0 };
        var result = body.Run(CreateContext(data, persistence));

        ((ForEachIterationContext)result.BranchValues![0]).Index.ShouldBe(1);

        // The drained iteration's scope — including descendant scopes from nested
        // containers — must be gone; unrelated scopes must survive.
        data.IterationStepOutputs.ShouldNotContainKey(completedScope);
        data.IterationStepOutputs.ShouldNotContainKey(nestedScope);
        data.IterationStepOutputs.ShouldContainKey(siblingContainerScope);
        data.IterationLastCompletedStepId.ShouldNotContainKey(completedScope);
        data.IterationLastCompletedStepId.ShouldNotContainKey(nestedScope);
    }

    [Fact]
    public void Run_Sequential_Completion_PrunesFinalIterationScope()
    {
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.forEach").WithName("ForEach");
        var finalScope = $"{stepConfig.Id:N}:1";

        var data = CreateData();
        data.IterationStepOutputs[finalScope] = new() { [Guid.NewGuid()] = new() { ["message"] = "iter-1" } };
        data.IterationLastCompletedStepId[finalScope] = Guid.NewGuid();

        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b" }, stepConfig: stepConfig);
        var persistence = new IteratorPersistenceData { ChildrenActive = true, Index = 1 };
        var result = body.Run(CreateContext(data, persistence));

        result.Proceed.ShouldBeTrue();
        data.IterationStepOutputs.ShouldBeEmpty();
        data.IterationLastCompletedStepId.ShouldBeEmpty();
    }

    [Fact]
    public void Run_Parallel_Completion_PrunesAllIterationScopes()
    {
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.forEach").WithName("ForEach");
        var scope0 = $"{stepConfig.Id:N}:0";
        var scope2 = $"{stepConfig.Id:N}:2";
        var nestedScope = $"{scope2}/{Guid.NewGuid():N}:0";

        var data = CreateData();
        data.IterationStepOutputs[scope0] = new() { [Guid.NewGuid()] = new() { ["message"] = "iter-0" } };
        data.IterationStepOutputs[scope2] = new() { [Guid.NewGuid()] = new() { ["message"] = "iter-2" } };
        data.IterationStepOutputs[nestedScope] = new() { [Guid.NewGuid()] = new() { ["message"] = "nested" } };
        data.IterationLastCompletedStepId[scope0] = Guid.NewGuid();

        var body = CreateBody(
            new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = true },
            stepConfig: stepConfig);
        var persistence = new IteratorPersistenceData { ChildrenActive = true };
        var result = body.Run(CreateContext(data, persistence));

        result.Proceed.ShouldBeTrue();
        data.IterationStepOutputs.ShouldBeEmpty();
        data.IterationLastCompletedStepId.ShouldBeEmpty();
    }

    private static ConditionSet SingleFilter(string left, ConditionOperator op, string right)
        => new()
        {
            Groups =
            [
                new ConditionGroup
                {
                    Conditions = [new Condition { LeftOperand = left, Operator = op, RightOperand = right }],
                },
            ],
        };

    [Fact]
    public void Run_TracksStepRunWithIterationTotal()
    {
        StepRun? saved = null;
        _runRepo.Setup(r => r.AddStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()))
            .Callback<StepRun, CancellationToken>((sr, _) => saved = sr)
            .ReturnsAsync((StepRun sr, CancellationToken _) => sr);

        var body = CreateBody(new ForEachControlFlowSettings { Collection = "a, b, c", RunParallel = true });
        body.Run(CreateContext());

        saved.ShouldNotBeNull();
        saved.IterationTotal.ShouldBe(3);
    }

    private ForEachContainerStepBody CreateBody(
        ForEachControlFlowSettings settings,
        IReadOnlyList<ContainerBranchEdge>? branchEdges = null,
        StepConfiguration? stepConfig = null)
    {
        stepConfig ??= new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.forEach").WithName("ForEach");
        return new ForEachContainerStepBody(
            stepConfig,
            settings,
            _collectionCache,
            _hydrationCache,
            _conditionEvaluator,
            _runRepo.Object,
            branchEdges ?? Array.Empty<ContainerBranchEdge>());
    }

    private static AutomationWorkflowData CreateData() => new()
    {
        RunId = Guid.NewGuid(),
        AutomationId = Guid.NewGuid(),
        TriggerOutput = [],
    };

    private static IStepExecutionContext CreateContext(
        AutomationWorkflowData? data = null,
        object? persistenceData = null)
    {
        data ??= CreateData();
        var context = new Mock<IStepExecutionContext>();
        var workflow = new Mock<WorkflowInstance>();
        workflow.Object.Data = data;
        context.Setup(c => c.Workflow).Returns(workflow.Object);
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        context.Setup(c => c.PersistenceData).Returns(persistenceData!);
        context.Setup(c => c.ExecutionPointer).Returns(new ExecutionPointer { Id = Guid.NewGuid().ToString() });
        return context.Object;
    }
}
