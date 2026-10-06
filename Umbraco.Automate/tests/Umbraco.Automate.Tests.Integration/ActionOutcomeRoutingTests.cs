// S2 — The run follows the exit the action picked (docs/plans/action-outcomes/STORIES.md)
// Also covers S4 AC8 (run time uses saved settings) and S6 AC5–AC6 ("Any result" lines).
//
// Real WorkflowCore host and compiler (see ActionRoutingHarness). Automation under test: a
// yes/no step, "yes" → A, "no" → B.

using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Extensions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Testing.Builders;

namespace Umbraco.Automate.Tests.Integration;

[Collection("WorkflowHost")]
public class ActionOutcomeRoutingTests : IAsyncLifetime
{
    private ActionRoutingHarness _harness = null!;

    public async Task InitializeAsync()
        => _harness = await ActionRoutingHarness.StartAsync(deps =>
        [
            new YesNoAction(deps),
            new PlainAction(deps),
            new TwoDefaultsAction(deps),
            new TrueFalseNoDefaultAction(deps),
            new ThrowingOutcomesAction(deps),
            new TimeoutOutcomesAction(deps),
            new BoundOptionsAction(deps),
            new FailingAction(deps),
        ]);

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    #region Given the action returns outcome "yes"

    [Fact]
    public async Task Run_ActionReturnsYes_RunsStepA()
    {
        var automation = YesNo(returns: "yes");

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.A.Id).Single().Status.ShouldBe(StepRunStatus.Completed);
    }

    [Fact]
    public async Task Run_ActionReturnsYes_DoesNotRunStepB()
    {
        var automation = YesNo(returns: "yes");

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.B.Id).ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ActionReturnsYes_RecordsBranchOutcomeYes()
    {
        var automation = YesNo(returns: "yes");

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.BranchOutcomeOf(automation.Decide.Id).ShouldBe("yes");
    }

    #endregion

    #region Given the action returns success with no outcome

    [Fact]
    public async Task Run_ActionReturnsNoOutcome_RunsDefaultStepB()
    {
        var automation = YesNo(returns: null);

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.B.Id).Single().Status.ShouldBe(StepRunStatus.Completed);
    }

    [Fact]
    public async Task Run_ActionReturnsNoOutcome_RecordsBranchOutcomeNo()
    {
        var automation = YesNo(returns: null);

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.BranchOutcomeOf(automation.Decide.Id).ShouldBe("no");
    }

    #endregion

    #region Given an action that declares nothing, with an unnamed line to C

    [Fact]
    public async Task Run_ActionDeclaringNothing_RunsStepC()
    {
        var plain = Step("test.plain");
        var c = Log("c");
        var automation = Build(plain, [c], (plain, c, null));

        var run = await _harness.RunToCompletionAsync(automation);

        run.For(c.Id).Single().Status.ShouldBe(StepRunStatus.Completed);
    }

    #endregion

    #region Given an unnamed ("Any result") line to C from the yes/no step

    [Fact]
    public async Task Run_AnyResultLine_FiresOnNamedOutcome()
    {
        var automation = YesNo(returns: "yes", anyResultLine: true);

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.C!.Id).Single().Status.ShouldBe(StepRunStatus.Completed);
    }

    [Fact]
    public async Task Run_AnyResultLine_FiresOnDefaultOutcome()
    {
        var automation = YesNo(returns: null, anyResultLine: true);

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.C!.Id).Single().Status.ShouldBe(StepRunStatus.Completed);
    }

    #endregion

    #region Given a bound dynamic step published with only an "other" exit

    [Fact]
    public async Task Run_BoundOptionsResolveToAB_ActionReturnsA_WarnsItIsUndeclared()
    {
        // The saved options are a binding, so the saved declaration is just [other]. At run time the
        // binding resolves to "a,b" and the action returns "a". Outcomes come from the saved
        // settings, so "a" is undeclared; had they come from the bound settings it would be declared.
        var (bound, automation) = BoundOptions();

        var run = await _harness.RunToCompletionAsync(automation, new() { ["options"] = "a,b" });

        run.For(bound.Id).Single().LogEntries
            .ShouldContain(e => e.Message == "Action returned outcome 'a', which it does not declare.");
    }

    [Fact]
    public async Task Run_BoundOptionsResolveToAB_ActionReturnsA_StillRoutesOnA()
    {
        var (bound, automation) = BoundOptions();

        var run = await _harness.RunToCompletionAsync(automation, new() { ["options"] = "a,b" });

        run.BranchOutcomeOf(bound.Id).ShouldBe("a");
    }

    private static (StepConfiguration Step, Automation Automation) BoundOptions()
    {
        var bound = Step("test.boundOptions", new() { ["options"] = "${ trigger.options }" });
        var other = Log("other");
        return (bound, Build(bound, [other], (bound, other, "other")));
    }

    #endregion

    #region Sad path

    [Fact]
    public async Task Run_UndeclaredOutcome_LogsWarning()
    {
        var automation = YesNo(returns: "maybe");

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.Decide.Id).Single().LogEntries
            .ShouldContain(e => e.Level == ActionLogLevel.Warning
                && e.Message == "Action returned outcome 'maybe', which it does not declare.");
    }

    [Fact]
    public async Task Run_UndeclaredOutcome_StillRunsAnyResultLine()
    {
        var automation = YesNo(returns: "maybe", anyResultLine: true);

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.C!.Id).Single().Status.ShouldBe(StepRunStatus.Completed);
    }

    [Fact]
    public async Task Run_UndeclaredOutcome_DoesNotRunNamedExits()
    {
        var automation = YesNo(returns: "maybe");

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.StepRuns.Select(s => s.StepId).ShouldNotContain(id => id == automation.A.Id || id == automation.B.Id);
    }

    [Fact]
    public async Task Run_BrokenDeclaration_FailsTheStep()
    {
        var step = Step("test.twoDefaults");
        var automation = Build(step, [], (step, null, null));

        var run = await _harness.RunToCompletionAsync(automation);

        run.For(step.Id).Single().Error!.ShouldStartWith("Action 'test.twoDefaults' declares invalid outcomes:");
    }

    [Fact]
    public async Task Run_BrokenDeclarationWithRetry_IsNotRetried()
    {
        var step = Step("test.twoDefaults");
        step.ErrorBehavior = StepErrorBehavior.Retry;
        step.MaxRetries = 3;
        var automation = Build(step, [], (step, null, null));

        var run = await _harness.RunToCompletionAsync(automation);

        run.For(step.Id).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Run_NoOutcomeAndNoDefault_FailsTheStep()
    {
        var step = Step("test.trueFalseNoDefault");
        var automation = Build(step, [], (step, null, null));

        var run = await _harness.RunToCompletionAsync(automation);

        run.For(step.Id).Single().Error.ShouldBe("Action 'test.trueFalseNoDefault' must return one of its declared outcomes.");
    }

    [Fact]
    public async Task Run_GetOutcomesAsyncThrows_FailsTheStepWithValidationCategory()
    {
        var step = Step("test.throwingOutcomes");
        var automation = Build(step, [], (step, null, null));

        var run = await _harness.RunToCompletionAsync(automation);

        run.For(step.Id).Single().ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task Run_GetOutcomesAsyncThrowsUncancelledTaskCanceled_FailsTheStepOnce()
    {
        // e.g. an HTTP timeout while listing options: not a real cancellation, so it must not escape.
        var step = Step("test.timeoutOutcomes");
        var automation = Build(step, [], (step, null, null));

        var run = await _harness.RunToCompletionAsync(automation);

        run.For(step.Id).ShouldHaveSingleItem().ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task Run_ActionFails_RunsNeitherExit()
    {
        var failing = Step("test.failing");
        var a = Log("a");
        var b = Log("b");
        var automation = Build(failing, [a, b], (failing, a, "yes"), (failing, b, "no"));

        var run = await _harness.RunToCompletionAsync(automation);

        run.StepRuns.Select(s => s.StepId).ShouldNotContain(id => id == a.Id || id == b.Id);
    }

    #endregion

    #region Automation building

    private sealed record YesNoAutomation(Automation Automation, StepConfiguration Decide, StepConfiguration A, StepConfiguration B, StepConfiguration? C);

    private static YesNoAutomation YesNo(string? returns, bool anyResultLine = false)
    {
        var decide = Step("test.yesNo", new() { ["returns"] = returns });
        var a = Log("a");
        var b = Log("b");
        var c = anyResultLine ? Log("c") : null;

        var lines = new List<(StepConfiguration, StepConfiguration?, string?)> { (decide, a, "yes"), (decide, b, "no") };
        if (c is not null)
        {
            lines.Add((decide, c, null));
        }

        var others = new List<StepConfiguration> { a, b };
        if (c is not null)
        {
            others.Add(c);
        }

        return new YesNoAutomation(Build(decide, others, [.. lines]), decide, a, b, c);
    }

    private static Automation Build(
        StepConfiguration first,
        IEnumerable<StepConfiguration> others,
        params (StepConfiguration Source, StepConfiguration? Target, string? Outcome)[] lines)
    {
        var builder = new AutomationBuilder()
            .WithAlias($"test-action-outcome-{Guid.NewGuid():N}")
            .WithName("test-action-outcome")
            .WithManualTrigger()
            .AddStep(first);

        foreach (var step in others)
        {
            builder.AddStep(step);
        }

        builder.WithTriggerConnection(first.Id);
        foreach (var (source, target, outcome) in lines.Where(l => l.Target is not null))
        {
            builder.WithConnection(source.Id, target!.Id, outcome);
        }

        return builder.Build();
    }

    private static StepConfiguration Step(string alias, Dictionary<string, object?>? settings = null) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = alias,
        Name = alias,
        Alias = alias.Replace('.', '_'),
        Settings = settings ?? [],
    };

    private static StepConfiguration Log(string alias) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.logMessage",
        Name = alias,
        Alias = alias,
        Settings = new Dictionary<string, object?> { ["message"] = alias, ["logLevel"] = "Information" },
    };

    #endregion

    #region Test actions

    internal sealed class RoutingSettings
    {
        public string? Returns { get; set; }

        [Field(Label = "Options", SupportsBindings = true)]
        public string? Options { get; set; }
    }

    private static ActionResult Result(ActionContext context)
        => (context.Settings as RoutingSettings)?.Returns is { } outcome
            ? ActionResult.SuccessWithOutcome(outcome)
            : ActionResult.Success();

    [Action("test.yesNo", "Yes or no")]
    private sealed class YesNoAction(ActionInfrastructure infrastructure) : ActionBase<RoutingSettings>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes()
            => [new StepOutcome("yes", "Yes"), new StepOutcome("no", "No") { IsDefault = true }];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(Result(context));
    }

    [Action("test.plain", "Plain")]
    private sealed class PlainAction(ActionInfrastructure infrastructure) : ActionBase<RoutingSettings>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.Success());
    }

    [Action("test.twoDefaults", "Two defaults")]
    private sealed class TwoDefaultsAction(ActionInfrastructure infrastructure) : ActionBase<RoutingSettings>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes()
            => [new StepOutcome("a", "A") { IsDefault = true }, new StepOutcome("b", "B") { IsDefault = true }];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.Success());
    }

    [Action("test.trueFalseNoDefault", "True or false")]
    private sealed class TrueFalseNoDefaultAction(ActionInfrastructure infrastructure) : ActionBase<RoutingSettings>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes()
            => [new StepOutcome("true", "True"), new StepOutcome("false", "False")];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.Success());
    }

    [Action("test.throwingOutcomes", "Throwing outcomes")]
    private sealed class ThrowingOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<RoutingSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            RoutingSettings? settings, CancellationToken cancellationToken)
            => throw new InvalidOperationException("The option source is unavailable.");

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.Success());
    }

    /// <summary>
    /// Dynamic outcomes from a comma-separated options string. While the options are still a
    /// binding expression nothing can be listed, so the only exit is the default "other".
    /// </summary>
    [Action("test.boundOptions", "Bound options")]
    private sealed class BoundOptionsAction(ActionInfrastructure infrastructure) : ActionBase<RoutingSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            RoutingSettings? settings, CancellationToken cancellationToken)
        {
            var options = settings?.Options;
            var listed = string.IsNullOrWhiteSpace(options) || options.ContainsBinding()
                ? []
                : options.Split(',').Select(o => new StepOutcome(o, o)).ToList();

            return Task.FromResult<IReadOnlyList<StepOutcome>>([.. listed, new StepOutcome("other", "Other") { IsDefault = true }]);
        }

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.SuccessWithOutcome(
                ((RoutingSettings)context.Settings!).Options!.Split(',')[0]));
    }

    [Action("test.timeoutOutcomes", "Timeout outcomes")]
    private sealed class TimeoutOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<RoutingSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            RoutingSettings? settings, CancellationToken cancellationToken)
            => throw new TaskCanceledException("The option source timed out.");

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.Success());
    }

    [Action("test.failing", "Failing")]
    private sealed class FailingAction(ActionInfrastructure infrastructure) : ActionBase<RoutingSettings>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes()
            => [new StepOutcome("yes", "Yes"), new StepOutcome("no", "No") { IsDefault = true }];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(ActionResult.Failed(new InvalidOperationException("boom"), StepRunErrorCategory.Validation));
    }

    #endregion
}
