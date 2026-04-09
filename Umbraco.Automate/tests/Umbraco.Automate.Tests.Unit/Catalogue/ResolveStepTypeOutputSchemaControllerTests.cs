using Json.Schema;
using Json.Schema.Generation;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using ActionContext = Umbraco.Automate.Core.Actions.ActionContext;
using ActionResult = Umbraco.Automate.Core.Actions.ActionResult;

namespace Umbraco.Automate.Tests.Unit.Catalogue;

public class ResolveStepTypeOutputSchemaControllerTests
{
    private static readonly ActionInfrastructure ActionDeps = CreateActionInfrastructure();

    [Fact]
    public async Task ResolveOutputSchema_UnknownAlias_Returns404()
    {
        var controller = CreateController();

        var result = await controller.ResolveOutputSchema(
            "nonexistent.alias",
            new ResolveOutputSchemaRequestModel());

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ResolveOutputSchema_StaticAction_ReturnsStaticSchema()
    {
        var action = new StaticAction(ActionDeps);
        var controller = CreateController(actions: [action]);

        var result = await controller.ResolveOutputSchema(
            "test.static",
            new ResolveOutputSchemaRequestModel());

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var schema = okResult.Value.ShouldBeOfType<Dictionary<string, object?>>();
        schema.ShouldContainKey("properties");
    }

    [Fact]
    public async Task ResolveOutputSchema_DynamicAction_WithSettings_ReturnsDynamicSchema()
    {
        var action = new DynamicAction(ActionDeps);
        var controller = CreateController(actions: [action]);

        var result = await controller.ResolveOutputSchema(
            "test.dynamic",
            new ResolveOutputSchemaRequestModel
            {
                Settings = new Dictionary<string, object?> { ["schemaType"] = "detailed" },
            });

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        var schema = okResult.Value.ShouldBeOfType<Dictionary<string, object?>>();
        schema.ShouldContainKey("properties");
    }

    [Fact]
    public async Task ResolveOutputSchema_DynamicAction_EmptySettings_ReturnsNull()
    {
        var action = new DynamicAction(ActionDeps);
        var controller = CreateController(actions: [action]);

        var result = await controller.ResolveOutputSchema(
            "test.dynamic",
            new ResolveOutputSchemaRequestModel());

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveOutputSchema_ActionWithNoOutput_ReturnsNull()
    {
        var action = new NoOutputAction(ActionDeps);
        var controller = CreateController(actions: [action]);

        var result = await controller.ResolveOutputSchema(
            "test.noOutput",
            new ResolveOutputSchemaRequestModel());

        var okResult = result.ShouldBeOfType<OkObjectResult>();
        okResult.Value.ShouldBeNull();
    }

    private static ResolveStepTypeOutputSchemaController CreateController(
        IAction[]? actions = null,
        IControlFlow[]? controlFlows = null,
        ITrigger[]? triggers = null)
    {
        return new ResolveStepTypeOutputSchemaController(
            new ActionCollection(() => actions ?? []),
            new ControlFlowCollection(() => controlFlows ?? []),
            new TriggerCollection(() => triggers ?? []));
    }

    private static ActionInfrastructure CreateActionInfrastructure()
    {
        var resolver = new Mock<IEditableModelResolver>();
        resolver.Setup(r => r.ResolveModel<DynamicSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<EditableModelSchema?>()))
            .Returns((string _, object? data, EditableModelSchema? _) =>
            {
                if (data is not Dictionary<string, object?> dict)
                {
                    return null;
                }

                return new DynamicSettings
                {
                    SchemaType = dict.TryGetValue("schemaType", out var v) ? v?.ToString() : null,
                };
            });
        return new ActionInfrastructure(resolver.Object);
    }

    #region Test Doubles

    private class StaticOutput
    {
        public string Name { get; set; } = string.Empty;
    }

    [Action("test.static", "Static Action")]
    private class StaticAction(ActionInfrastructure infrastructure)
        : ActionBase<object, StaticOutput>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    [Action("test.noOutput", "No Output Action")]
    private class NoOutputAction(ActionInfrastructure infrastructure)
        : ActionBase<object>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();
    }

    private class DynamicSettings
    {
        public string? SchemaType { get; set; }
    }

    [Action("test.dynamic", "Dynamic Action")]
    private class DynamicAction(ActionInfrastructure infrastructure)
        : DynamicOutputActionBase<DynamicSettings>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<JsonSchema?> GetOutputSchemaAsync(
            DynamicSettings? settings, CancellationToken cancellationToken)
        {
            if (settings?.SchemaType is null)
            {
                return Task.FromResult<JsonSchema?>(null);
            }

            var builder = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("detailedField", new JsonSchemaBuilder().Type(SchemaValueType.String)));

            return Task.FromResult<JsonSchema?>(builder.Build());
        }
    }

    #endregion
}
