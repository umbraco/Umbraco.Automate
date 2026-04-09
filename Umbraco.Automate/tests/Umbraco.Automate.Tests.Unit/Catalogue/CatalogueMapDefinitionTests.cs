using Json.Schema;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Web.Api.Management.Catalogue.Mapping;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Catalogue;

public class CatalogueMapDefinitionTests
{
    private static readonly ActionInfrastructure ActionDeps = new(Mock.Of<IEditableModelResolver>());

    private readonly IUmbracoMapper _mapper;

    public CatalogueMapDefinitionTests()
    {
        var mapDefinition = new CatalogueMapDefinition();
        var definitions = new MapDefinitionCollection(() => [mapDefinition]);
        _mapper = new UmbracoMapper(
            definitions,
            Mock.Of<ICoreScopeProvider>(),
            Mock.Of<ILogger<UmbracoMapper>>());
    }

    [Fact]
    public void MapToActionItem_StaticAction_HasDynamicOutputSchemaIsFalse()
    {
        var action = new StaticAction(ActionDeps);

        var result = _mapper.Map<IAction, ActionItemResponseModel>(action);

        result.ShouldNotBeNull();
        result.HasDynamicOutputSchema.ShouldBeFalse();
    }

    [Fact]
    public void MapToActionItem_DynamicAction_HasDynamicOutputSchemaIsTrue()
    {
        var action = new DynamicAction(ActionDeps);

        var result = _mapper.Map<IAction, ActionItemResponseModel>(action);

        result.ShouldNotBeNull();
        result.HasDynamicOutputSchema.ShouldBeTrue();
    }

    [Fact]
    public void MapToActionItem_StaticAction_OutputSchemaIsPopulated()
    {
        var action = new StaticAction(ActionDeps);

        var result = _mapper.Map<IAction, ActionItemResponseModel>(action);

        result.ShouldNotBeNull();
        result.OutputSchema.ShouldNotBeNull();
        result.OutputSchema.ShouldContainKey("properties");
    }

    [Fact]
    public void MapToActionItem_DynamicAction_OutputSchemaIsNull()
    {
        // DynamicOutputActionBase uses TOutput=object via ActionBase<TSettings>,
        // so static GetOutputSchema() returns null.
        var action = new DynamicAction(ActionDeps);

        var result = _mapper.Map<IAction, ActionItemResponseModel>(action);

        result.ShouldNotBeNull();
        result.OutputSchema.ShouldBeNull();
    }

    [Fact]
    public void MapToActionItem_MapsAllProperties()
    {
        var action = new StaticAction(ActionDeps);

        var result = _mapper.Map<IAction, ActionItemResponseModel>(action);

        result.ShouldNotBeNull();
        result.Alias.ShouldBe("test.static");
        result.Name.ShouldBe("Static Action");
        result.Description.ShouldBe("A test action");
        result.Group.ShouldBe("Testing");
        result.Icon.ShouldBe("icon-test");
        result.Type.ShouldBe("action");
    }

    #region Test Doubles

    private class StaticOutput
    {
        public string Name { get; set; } = string.Empty;
    }

    [Action("test.static", "Static Action", Description = "A test action", Group = "Testing", Icon = "icon-test")]
    private class StaticAction(ActionInfrastructure infrastructure)
        : ActionBase<object, StaticOutput>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => Task.FromResult(Success(new StaticOutput()));
    }

    [Action("test.dynamic", "Dynamic Action")]
    private class DynamicAction(ActionInfrastructure infrastructure)
        : DynamicOutputActionBase<object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<JsonSchema?> GetOutputSchemaAsync(
            object? settings, CancellationToken cancellationToken)
            => Task.FromResult<JsonSchema?>(null);
    }

    #endregion
}
