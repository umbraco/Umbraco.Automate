// S3 — See and connect one exit per outcome: catalogue (docs/plans/action-outcomes/STORIES.md)
// Also covers S7 AC3 (catalogue labels returned raw).

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Catalogue;

public class CatalogueOutcomesMappingTests
{
    private static readonly ActionInfrastructure ActionDeps = new(Mock.Of<IEditableModelResolver>());

    private readonly IUmbracoMapper _mapper;

    public CatalogueOutcomesMappingTests()
    {
        var mapDefinition = new CatalogueMapDefinition(NullLogger<CatalogueMapDefinition>.Instance);
        var definitions = new MapDefinitionCollection(() => [mapDefinition]);
        _mapper = new UmbracoMapper(
            definitions,
            Mock.Of<ICoreScopeProvider>(),
            Mock.Of<ILogger<UmbracoMapper>>());
    }

    private ActionItemResponseModel MapAction(IAction action)
        => _mapper.Map<IAction, ActionItemResponseModel>(action)!;

    #region Given the yes/no action

    [Fact]
    public void Map_YesNoAction_OutcomeKeysAreYesThenNo()
    {
        var result = MapAction(new YesNoAction(ActionDeps));

        result.Outcomes.Select(o => o.Key).ShouldBe(["yes", "no"]);
    }

    [Fact]
    public void Map_YesNoAction_NoIsTheDefault()
    {
        var result = MapAction(new YesNoAction(ActionDeps));

        result.Outcomes.Where(o => o.IsDefault).Select(o => o.Key).ShouldBe(["no"]);
    }

    [Fact]
    public void Map_YesNoAction_HasDynamicOutcomesIsFalse()
    {
        var result = MapAction(new YesNoAction(ActionDeps));

        result.HasDynamicOutcomes.ShouldBeFalse();
    }

    [Fact]
    public void Map_KeyLabel_IsMappedUntranslated()
    {
        var result = MapAction(new KeyLabelAction(ActionDeps));

        result.Outcomes.Single().Label.ShouldBe("#uaOutcomes_found");
    }

    [Fact]
    public void Map_LiteralLabel_IsMappedAsIs()
    {
        var result = MapAction(new LiteralLabelAction(ActionDeps));

        result.Outcomes.Single().Label.ShouldBe("Breaking news");
    }

    #endregion

    #region Given an action whose GetOutcomes throws

    [Fact]
    public void Map_ThrowingAction_OutcomesIsEmpty()
    {
        var result = _mapper.MapEnumerable<IAction, ActionItemResponseModel>(
            [new ThrowingOutcomesAction(ActionDeps), new YesNoAction(ActionDeps)]);

        result[0].Outcomes.ShouldBeEmpty();
    }

    [Fact]
    public void Map_ThrowingAction_OtherActionsStillMapTheirOutcomes()
    {
        var result = _mapper.MapEnumerable<IAction, ActionItemResponseModel>(
            [new ThrowingOutcomesAction(ActionDeps), new YesNoAction(ActionDeps)]);

        result[1].Outcomes.Select(o => o.Key).ShouldBe(["yes", "no"]);
    }

    #endregion

    #region Given an action whose outcome list contains a null item

    [Fact]
    public void Map_ActionWithNullOutcomeItem_SkipsTheNullAndKeepsTheRest()
    {
        var result = MapAction(new NullItemOutcomesAction(ActionDeps));

        result.Outcomes.Select(o => o.Key).ShouldBe(["yes", "no"]);
    }

    #endregion

    #region Given the dynamic options action

    [Fact]
    public void Map_DynamicAction_HasDynamicOutcomesIsTrue()
    {
        var result = MapAction(new DynamicOutcomesAction(ActionDeps));

        result.HasDynamicOutcomes.ShouldBeTrue();
    }

    #endregion

    #region Given an action that declares nothing

    [Fact]
    public void Map_ActionDeclaringNothing_OutcomesIsEmptyNotNull()
    {
        var result = MapAction(new PlainAction(ActionDeps));

        result.Outcomes.ShouldBeEmpty();
    }

    [Fact]
    public void Map_ActionDeclaringNothing_HasDynamicOutcomesIsFalse()
    {
        var result = MapAction(new PlainAction(ActionDeps));

        result.HasDynamicOutcomes.ShouldBeFalse();
    }

    #endregion

    #region Given a trigger

    [Fact]
    public void Map_Trigger_OutcomesIsEmpty()
    {
        // Even a trigger that (wrongly) declares outcomes must map to none.
        var trigger = new Mock<ITrigger>();
        trigger.Setup(t => t.GetOutcomes()).Returns([new StepOutcome("x", "X")]);
        trigger.Setup(t => t.HasDynamicOutcomes).Returns(true);

        var result = _mapper.Map<ITrigger, TriggerItemResponseModel>(trigger.Object)!;

        result.Outcomes.ShouldBeEmpty();
    }

    #endregion

    #region Test Doubles

    [Action("test.yesno", "Yes/No Action")]
    private class YesNoAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes() =>
        [
            new StepOutcome("yes", "Yes"),
            new StepOutcome("no", "No") { IsDefault = true },
        ];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.keylabel", "Key Label Action")]
    private class KeyLabelAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes() => [new StepOutcome("found", "#uaOutcomes_found")];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.literallabel", "Literal Label Action")]
    private class LiteralLabelAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes() => [new StepOutcome("news", "Breaking news")];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.throwingoutcomes", "Throwing Outcomes Action")]
    private class ThrowingOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes() => throw new InvalidOperationException("Broken declaration.");

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.nulloutcomeitem", "Null Outcome Item Action")]
    private class NullItemOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public override IReadOnlyList<StepOutcome> GetOutcomes()
            => [new StepOutcome("yes", "Yes"), null!, new StepOutcome("no", "No")];

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.dynamicoutcomes", "Dynamic Outcomes Action")]
    private class DynamicOutcomesAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public override bool HasDynamicOutcomes => true;

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.plain", "Plain Action")]
    private class PlainAction(ActionInfrastructure infrastructure) : ActionBase<object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    #endregion
}
