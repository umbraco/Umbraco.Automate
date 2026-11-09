// Issue #558 — an If step whose condition uses an unknown binding filter alias ends the run
// instead of comparing the unfiltered value, and is not retried.
//
// Real WorkflowCore host and compiler (see ActionRoutingHarness): control-flow steps have no
// failure handling of their own and fall back to the workflow's default Retry behaviour, which
// AutomateRetryHandler turns into Terminate for this error.

using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Testing.Builders;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Integration;

[Collection("WorkflowHost")]
public class UnknownBindingFilterControlFlowTests : IAsyncLifetime
{
    private ActionRoutingHarness _harness = null!;

    public async Task InitializeAsync()
        => _harness = await ActionRoutingHarness.StartAsync(_ => []);

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Run_IfConditionWithUnknownFilter_TerminatesTheWorkflow()
    {
        var automation = IfWithCondition("${ trigger.country | lowercse }");

        var run = await _harness.RunToCompletionAsync(automation.Automation, new Dictionary<string, object?> { ["country"] = "DK" });

        run.WorkflowStatus.ShouldBe(WorkflowStatus.Terminated);
    }

    [Fact]
    public async Task Run_IfConditionWithUnknownFilter_DoesNotRunEitherBranch()
    {
        var automation = IfWithCondition("${ trigger.country | lowercse }");

        var run = await _harness.RunToCompletionAsync(automation.Automation, new Dictionary<string, object?> { ["country"] = "DK" });

        run.StepRuns.Where(s => s.StepId == automation.OnTrue.Id || s.StepId == automation.OnFalse.Id).ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_IfConditionWithKnownFilter_RunsTheMatchingBranch()
    {
        var automation = IfWithCondition("${ trigger.country | lowercase }");

        var run = await _harness.RunToCompletionAsync(automation.Automation, new Dictionary<string, object?> { ["country"] = "DK" });

        run.For(automation.OnTrue.Id).Count.ShouldBe(1);
    }

    private static (Automation Automation, StepConfiguration OnTrue, StepConfiguration OnFalse) IfWithCondition(string left)
    {
        var condition = new ConditionSet
        {
            Groups =
            [
                new ConditionGroup
                {
                    Conditions = [new Condition { LeftOperand = left, Operator = ConditionOperator.Equals, RightOperand = "dk" }],
                },
            ],
        };

        var decide = new StepConfigurationBuilder()
            .WithActionAlias("umbracoAutomate.if").WithName("Is Danish").WithAlias("isDanish")
            .WithSetting("conditions", condition)
            .Build();
        var onTrue = Log("onTrue");
        var onFalse = Log("onFalse");

        var automation = new AutomationBuilder()
            .WithAlias($"test-unknown-filter-{Guid.NewGuid():N}")
            .WithName("test-unknown-filter")
            .WithManualTrigger()
            .AddStep(decide)
            .AddStep(onTrue)
            .AddStep(onFalse)
            .WithTriggerConnection(decide.Id)
            .WithConnection(decide.Id, onTrue.Id, outcome: "true")
            .WithConnection(decide.Id, onFalse.Id, outcome: "false")
            .Build();

        return (automation, onTrue, onFalse);
    }

    private static StepConfiguration Log(string alias) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.logMessage",
        Name = alias,
        Alias = alias,
        Settings = new Dictionary<string, object?> { ["message"] = alias, ["logLevel"] = "Information" },
    };
}
