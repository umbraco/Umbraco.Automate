// S9 — Content and media actions offer named exits: routing (docs/plans/action-outcomes/STORIES.md)
//
// Automation under test: Get Content with "success" → A and "notFound" → B. A real GetContentAction
// needs the published cache, the Umbraco context, a URL provider and property conversion, none of
// which the real-WorkflowCore harness can supply. These tests use a stand-in that declares the same
// outcomes as GetContentAction (its own keys and default) and returns the same outcome per case, so
// they check what the run does with that declaration. The real action's declaration and its return
// values are covered by BuiltInActionOutcomeTests in the unit project.

using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Testing.Builders;

namespace Umbraco.Automate.Tests.Integration;

[Collection("WorkflowHost")]
public class BuiltInActionOutcomeRoutingTests : IAsyncLifetime
{
    private ActionRoutingHarness _harness = null!;

    public async Task InitializeAsync()
        => _harness = await ActionRoutingHarness.StartAsync(deps => [new GetContentLikeAction(deps)]);

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    #region Given the content exists

    [Fact]
    public async Task Run_ContentExists_RunsSuccessStepA()
    {
        var automation = GetContent(missing: false);

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.A.Id).Single().Status.ShouldBe(StepRunStatus.Completed);
    }

    [Fact]
    public async Task Run_ContentExists_DoesNotRunNotFoundStepB()
    {
        var automation = GetContent(missing: false);

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.B.Id).ShouldBeEmpty();
    }

    #endregion

    #region Given the content doesn't exist

    [Fact]
    public async Task Run_ContentMissing_RunsNotFoundStepB()
    {
        var automation = GetContent(missing: true);

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.B.Id).Single().Status.ShouldBe(StepRunStatus.Completed);
    }

    [Fact]
    public async Task Run_ContentMissing_DoesNotRunSuccessStepA()
    {
        var automation = GetContent(missing: true);

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.A.Id).ShouldBeEmpty();
    }

    #endregion

    #region Sad path: automation saved before outcomes were declared

    [Fact]
    public async Task Run_LegacyUnnamedLineAndContentMissing_StillRunsStepC()
    {
        var automation = GetContent(missing: true, legacyLine: true);

        var run = await _harness.RunToCompletionAsync(automation.Automation);

        run.For(automation.C!.Id).Single().Status.ShouldBe(StepRunStatus.Completed);
    }

    #endregion

    private sealed record GetContentAutomation(Automation Automation, StepConfiguration A, StepConfiguration B, StepConfiguration? C);

    private static GetContentAutomation GetContent(bool missing, bool legacyLine = false)
    {
        var get = new StepConfiguration
        {
            Id = Guid.NewGuid(),
            ActionAlias = "test.getContentLike",
            Name = "Get content",
            Alias = "getContent",
            Settings = new Dictionary<string, object?> { ["missing"] = missing },
        };
        var a = Log("a");
        var b = Log("b");
        var c = legacyLine ? Log("c") : null;

        var builder = new AutomationBuilder()
            .WithAlias($"test-get-content-outcome-{Guid.NewGuid():N}")
            .WithName("test-get-content-outcome")
            .WithManualTrigger()
            .AddStep(get)
            .AddStep(a)
            .AddStep(b);

        if (c is not null)
        {
            builder.AddStep(c);
        }

        builder.WithTriggerConnection(get.Id)
            .WithConnection(get.Id, a.Id, GetContentAction.OutcomeSuccess)
            .WithConnection(get.Id, b.Id, GetContentAction.OutcomeNotFound);

        if (c is not null)
        {
            builder.WithConnection(get.Id, c.Id);
        }

        return new GetContentAutomation(builder.Build(), a, b, c);
    }

    private static StepConfiguration Log(string alias) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.logMessage",
        Name = alias,
        Alias = alias,
        Settings = new Dictionary<string, object?> { ["message"] = alias, ["logLevel"] = "Information" },
    };

    internal sealed class GetContentLikeSettings
    {
        public bool Missing { get; set; }
    }

    [Action("test.getContentLike", "Get content (stand-in)")]
    private sealed class GetContentLikeAction(ActionInfrastructure infrastructure) : ActionBase<GetContentLikeSettings>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes()
            =>
            [
                new StepOutcome(GetContentAction.OutcomeSuccess, "Found") { IsDefault = true },
                new StepOutcome(GetContentAction.OutcomeNotFound, "Not found"),
            ];

        // Like GetContentAction, a found item returns no outcome (the default applies); a missing one names notFound.
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult((context.Settings as GetContentLikeSettings)?.Missing == true
                ? ActionResult.SuccessWithOutcome(GetContentAction.OutcomeNotFound)
                : ActionResult.Success());
    }
}
