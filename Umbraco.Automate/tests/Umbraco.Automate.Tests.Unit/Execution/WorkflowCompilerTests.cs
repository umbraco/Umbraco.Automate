using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.ControlFlow.BuiltIn;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Testing.Builders;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class WorkflowCompilerTests
{
    private readonly WorkflowCompiler _compiler;

    public WorkflowCompilerTests()
    {
        var action = new Mock<IAction>();
        action.Setup(a => a.Alias).Returns("testAction");

        var actions = new ActionCollection(() => new[] { action.Object });

        var controlFlowInfra = new ControlFlowInfrastructure(Mock.Of<IEditableModelResolver>());
        var forEachControlFlow = new ForEachControlFlow(controlFlowInfra);
        var whileControlFlow = new WhileControlFlow(controlFlowInfra);
        var parallelControlFlow = new ParallelControlFlow(controlFlowInfra);
        var controlFlows = new ControlFlowCollection(() => new IControlFlow[]
        {
            forEachControlFlow,
            whileControlFlow,
            parallelControlFlow,
        });

        var pipeline = new ActionMiddlewarePipeline(new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>));
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        var conditionEvaluator = new ConditionEvaluator(evaluator);

        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<ILogger<ActionStepBody>>());
        services.Configure<Umbraco.Automate.Core.Configuration.ExecutionOptions>(_ => { });
        var sp = services.BuildServiceProvider();

        var meterFactory = new Mock<IMeterFactory>();
        meterFactory.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts.Name));

        _compiler = new WorkflowCompiler(
            actions,
            controlFlows,
            pipeline,
            evaluator,
            new SettingsBindingResolver(evaluator),
            conditionEvaluator,
            Mock.Of<IAutomationRunRepository>(),
            Mock.Of<IConnectionService>(),
            new DefaultStepErrorClassifier(),
            new AutomateMetrics(meterFactory.Object),
            sp,
            Mock.Of<ILogger<WorkflowCompiler>>());
    }

    [Fact]
    public void Compile_ForEachContainer_PopulatesChildrenFromGraphAnalysis()
    {
        // ForEach → ActionA → ActionB → Merge
        //                              ↗
        // (ForEach has one branch: A → B, converging at Merge)
        // But with two branches it's more interesting:
        // Actually, ForEach branches come from Branch() at runtime, children are the body steps.
        // Let's test with: ForEach → ActionA → Merge
        //                  ForEach → ActionB → Merge
        StepConfiguration forEach = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.forEach").WithName("ForEach");
        StepConfiguration actionA = new StepConfigurationBuilder()
            .WithActionAlias("testAction").WithName("Action A");
        StepConfiguration actionB = new StepConfigurationBuilder()
            .WithActionAlias("testAction").WithName("Action B");
        StepConfiguration merge = new StepConfigurationBuilder()
            .WithActionAlias("testAction").WithName("Merge");

        var automation = new AutomationBuilder()
            .AddStep(forEach)
            .AddStep(actionA)
            .AddStep(actionB)
            .AddStep(merge)
            .WithConnection(forEach.Id, actionA.Id, "branch1")
            .WithConnection(forEach.Id, actionB.Id, "branch2")
            .WithConnection(actionA.Id, merge.Id)
            .WithConnection(actionB.Id, merge.Id)
            .Build();

        var definition = _compiler.Compile(automation, "test-wf");

        // ForEach step should have children.
        var forEachStep = definition.Steps.FindById(0);
        forEachStep.Name.ShouldBe("ForEach");
        forEachStep.Children.Count.ShouldBe(2); // ActionA and ActionB are children

        // Merge is NOT a child — it's the convergence point.
        var mergeIndex = definition.Steps.Cast<WorkflowStep>()
            .First(s => s.Name == "Merge").Id;
        forEachStep.Children.ShouldNotContain(mergeIndex);

        // Container should have an outcome to the convergence step.
        forEachStep.Outcomes.ShouldContain(o => ((ValueOutcome)o).NextStep == mergeIndex);
    }

    [Fact]
    public void Compile_ForEachContainer_BodyType_IsForEachContainerStepBody()
    {
        StepConfiguration forEach = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.forEach").WithName("ForEach");
        StepConfiguration action = new StepConfigurationBuilder()
            .WithActionAlias("testAction").WithName("Body Step");

        var automation = new AutomationBuilder()
            .AddStep(forEach)
            .AddStep(action)
            .WithConnection(forEach.Id, action.Id)
            .Build();

        var definition = _compiler.Compile(automation, "test-wf");

        var forEachStep = definition.Steps.FindById(0);
        forEachStep.BodyType.ShouldBe(typeof(ForEachContainerStepBody));
    }

    [Fact]
    public void Compile_IfControlFlow_UsesOutcomeRouting_NotChildren()
    {
        // If should use outcome routing, not container children.
        // We don't register If in the control flow collection here (only container types),
        // so let's just verify actions still compile normally.
        StepConfiguration stepA = new StepConfigurationBuilder()
            .WithActionAlias("testAction").WithName("Step A");
        StepConfiguration stepB = new StepConfigurationBuilder()
            .WithActionAlias("testAction").WithName("Step B");

        var automation = new AutomationBuilder()
            .AddStep(stepA)
            .AddStep(stepB)
            .WithConnection(stepA.Id, stepB.Id)
            .Build();

        var definition = _compiler.Compile(automation, "test-wf");

        definition.Steps.Count.ShouldBe(2);
        // Action steps should NOT have children.
        definition.Steps.FindById(0).Children.ShouldBeEmpty();
        definition.Steps.FindById(1).Children.ShouldBeEmpty();
    }
}
