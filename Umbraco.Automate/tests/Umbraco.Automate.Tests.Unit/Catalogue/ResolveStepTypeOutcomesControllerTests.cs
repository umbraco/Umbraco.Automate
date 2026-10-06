// S4 — Outcomes driven by step settings: resolve endpoint (docs/plans/action-outcomes/STORIES.md)
// Also covers S7 AC3 (labels returned raw).

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Scoping;
using ActionContext = Umbraco.Automate.Core.Actions.ActionContext;
using ActionResult = Umbraco.Automate.Core.Actions.ActionResult;

namespace Umbraco.Automate.Tests.Unit.Catalogue;

public class ResolveStepTypeOutcomesControllerTests
{
    private const string ResolverMessage = "Validation failed for model 'test.dynamic':\nOptions is required";

    #region Given a dynamic action and options a, b

    [Fact]
    public async Task ResolveOutcomes_DynamicActionWithOptionsAB_ReturnsOk()
    {
        var result = await ResolveDynamicAsync("a,b");

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResolveOutcomes_DynamicActionWithOptionsAB_ReturnsAThenBThenOther()
    {
        var result = await ResolveDynamicAsync("a,b");

        OutcomesOf(result).Select(o => o.Key).ShouldBe(["a", "b", "other"]);
    }

    [Fact]
    public async Task ResolveOutcomes_DynamicActionWithOptionsAB_MarksOtherAsDefault()
    {
        var result = await ResolveDynamicAsync("a,b");

        OutcomesOf(result).Where(o => o.IsDefault).Select(o => o.Key).ShouldBe(["other"]);
    }

    #endregion

    #region Given a static yes/no action

    [Fact]
    public async Task ResolveOutcomes_StaticAction_ReturnsItsStaticList()
    {
        var result = await ResolveAsync("test.static", actions: [new StaticAction(CreateInfrastructure())]);

        OutcomesOf(result).Select(o => o.Key).ShouldBe(["yes", "no"]);
    }

    [Fact]
    public async Task ResolveOutcomes_StaticAction_MarksNoAsDefault()
    {
        var result = await ResolveAsync("test.static", actions: [new StaticAction(CreateInfrastructure())]);

        OutcomesOf(result).Where(o => o.IsDefault).Select(o => o.Key).ShouldBe(["no"]);
    }

    [Fact]
    public async Task ResolveOutcomes_ActionWithNullOutcomeItem_SkipsTheNullAndKeepsTheRest()
    {
        var result = await ResolveAsync("test.nullItem", actions: [new NullItemAction(CreateInfrastructure())]);

        OutcomesOf(result).Select(o => o.Key).ShouldBe(["yes", "no"]);
    }

    [Fact]
    public async Task ResolveOutcomes_StaticActionWithSettingsTheResolverRejects_StillReturnsOk()
    {
        var action = new StaticSettingsAction(CreateInfrastructure(resolverThrows: true));

        var result = await ResolveAsync("test.staticSettings", actions: [action]);

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResolveOutcomes_ActionDeclaringNothing_ReturnsEmptyList()
    {
        var result = await ResolveAsync("test.none", actions: [new NoOutcomeAction(CreateInfrastructure())]);

        OutcomesOf(result).ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveOutcomes_ActionReturningNullOutcomes_ReturnsEmptyList()
    {
        var result = await ResolveAsync("test.null", actions: [new NullOutcomeAction(CreateInfrastructure())]);

        OutcomesOf(result).ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveOutcomes_Trigger_ReturnsEmptyList()
    {
        var trigger = new Mock<ITrigger>();
        trigger.SetupGet(t => t.Alias).Returns("test.trigger");
        trigger.Setup(t => t.GetOutcomes()).Returns([new StepOutcome("x", "X")]);

        var result = await ResolveAsync("test.trigger", triggers: [trigger.Object]);

        OutcomesOf(result).ShouldBeEmpty();
    }

    #endregion

    #region Given outcome labels that are a #key and literal text

    [Fact]
    public async Task ResolveOutcomes_KeyLabel_IsReturnedUntranslated()
    {
        var result = await ResolveAsync("test.labels", actions: [new LabelAction(CreateInfrastructure())]);

        OutcomesOf(result).Single(o => o.Key == "found").Label.ShouldBe("#uaOutcomes_found");
    }

    [Fact]
    public async Task ResolveOutcomes_LiteralLabel_IsReturnedAsIs()
    {
        var result = await ResolveAsync("test.labels", actions: [new LabelAction(CreateInfrastructure())]);

        OutcomesOf(result).Single(o => o.Key == "news").Label.ShouldBe("Breaking news");
    }

    #endregion

    #region Sad path

    [Fact]
    public async Task ResolveOutcomes_UnknownAlias_ReturnsNotFound()
    {
        var result = await ResolveAsync("nope");

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ResolveOutcomes_InvalidSettings_ReturnsBadRequest()
    {
        var result = await ResolveRejectedAsync();

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ResolveOutcomes_InvalidSettings_ProblemTitleIsInvalidSettings()
    {
        var result = await ResolveRejectedAsync();

        ProblemOf(result).Title.ShouldBe("Invalid settings");
    }

    [Fact]
    public async Task ResolveOutcomes_InvalidSettings_ProblemDetailIsTheResolverMessage()
    {
        var result = await ResolveRejectedAsync();

        ProblemOf(result).Detail.ShouldBe(ResolverMessage);
    }

    [Fact]
    public async Task ResolveOutcomes_ActionOverrideThrowsInvalidOperation_PropagatesTheException()
    {
        var action = new ThrowingAction(CreateInfrastructure());

        var exception = await Record.ExceptionAsync(() => ResolveAsync("test.throwing", actions: [action]));

        exception.ShouldBeOfType<InvalidOperationException>();
    }

    #endregion

    private static Task<IActionResult> ResolveDynamicAsync(string options)
        => ResolveAsync(
            "test.dynamic",
            actions: [new DynamicAction(CreateInfrastructure())],
            settings: new Dictionary<string, object?> { ["options"] = options });

    private static Task<IActionResult> ResolveRejectedAsync()
        => ResolveAsync(
            "test.dynamic",
            actions: [new DynamicAction(CreateInfrastructure(resolverThrows: true))],
            settings: new Dictionary<string, object?> { ["options"] = "" });

    private static Task<IActionResult> ResolveAsync(
        string alias,
        IAction[]? actions = null,
        ITrigger[]? triggers = null,
        Dictionary<string, object?>? settings = null)
    {
        var controller = new ResolveStepTypeOutcomesController(
            new ActionCollection(() => actions ?? []),
            new ControlFlowCollection(() => []),
            new TriggerCollection(() => triggers ?? []),
            CreateMapper(),
            NullLogger<ResolveStepTypeOutcomesController>.Instance);

        return controller.ResolveOutcomes(alias, new ResolveOutcomesRequestModel { Settings = settings ?? [] });
    }

    private static IUmbracoMapper CreateMapper()
        => new UmbracoMapper(
            new MapDefinitionCollection(() => [new CatalogueMapDefinition(NullLogger<CatalogueMapDefinition>.Instance)]),
            Mock.Of<ICoreScopeProvider>(),
            Mock.Of<ILogger<UmbracoMapper>>());

    private static List<StepOutcomeResponseModel> OutcomesOf(IActionResult result)
        => result.ShouldBeOfType<OkObjectResult>().Value.ShouldBeOfType<List<StepOutcomeResponseModel>>();

    private static ProblemDetails ProblemOf(IActionResult result)
        => result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ProblemDetails>();

    private static ActionInfrastructure CreateInfrastructure(bool resolverThrows = false)
    {
        var resolver = new Mock<IEditableModelResolver>();
        var setup = resolver.Setup(r => r.ResolveModel<OutcomeSettings>(
            It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<EditableModelSchema?>()));

        if (resolverThrows)
        {
            setup.Throws(new SettingsResolutionException(ResolverMessage));
        }
        else
        {
            setup.Returns((string _, object? data, EditableModelSchema? _) =>
                data is Dictionary<string, object?> dict
                    ? new OutcomeSettings { Options = dict.GetValueOrDefault("options")?.ToString() }
                    : null);
        }

        return new ActionInfrastructure(resolver.Object);
    }

    #region Test Doubles

    private class OutcomeSettings
    {
        public string? Options { get; set; }
    }

    [Action("test.dynamic", "Dynamic Action")]
    private class DynamicAction(ActionInfrastructure infrastructure)
        : ActionBase<OutcomeSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            OutcomeSettings? settings, CancellationToken cancellationToken)
        {
            var keys = (settings?.Options ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries);
            IReadOnlyList<StepOutcome> outcomes =
            [
                .. keys.Select(key => new StepOutcome(key, key)),
                new StepOutcome("other", "Other") { IsDefault = true },
            ];
            return Task.FromResult(outcomes);
        }
    }

    [Action("test.throwing", "Throwing Action")]
    private class ThrowingAction(ActionInfrastructure infrastructure)
        : ActionBase<OutcomeSettings>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<IReadOnlyList<StepOutcome>> GetOutcomesAsync(
            OutcomeSettings? settings, CancellationToken cancellationToken)
            => throw new InvalidOperationException("bug in the action");
    }

    [Action("test.static", "Static Action")]
    private class StaticAction(ActionInfrastructure infrastructure)
        : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes()
            => [new StepOutcome("yes", "Yes"), new StepOutcome("no", "No") { IsDefault = true }];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.nullItem", "Null Item Action")]
    private class NullItemAction(ActionInfrastructure infrastructure)
        : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes()
            => [new StepOutcome("yes", "Yes"), null!, new StepOutcome("no", "No")];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.staticSettings", "Static With Settings Action")]
    private class StaticSettingsAction(ActionInfrastructure infrastructure)
        : ActionBase<OutcomeSettings>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes()
            => [new StepOutcome("yes", "Yes"), new StepOutcome("no", "No")];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.none", "No Outcome Action")]
    private class NoOutcomeAction(ActionInfrastructure infrastructure)
        : ActionBase<object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.null", "Null Outcome Action")]
    private class NullOutcomeAction(ActionInfrastructure infrastructure)
        : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes() => null!;

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.labels", "Label Action")]
    private class LabelAction(ActionInfrastructure infrastructure)
        : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes()
            => [new StepOutcome("found", "#uaOutcomes_found"), new StepOutcome("news", "Breaking news")];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    #endregion
}
