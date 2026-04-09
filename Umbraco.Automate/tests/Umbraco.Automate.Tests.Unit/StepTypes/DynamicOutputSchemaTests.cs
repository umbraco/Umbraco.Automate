using Json.Schema;
using Json.Schema.Generation;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Tests.Unit.StepTypes;

public class DynamicOutputSchemaTests
{
    private static readonly ActionInfrastructure ActionDeps = CreateActionInfrastructure();

    [Fact]
    public void StaticAction_DoesNotImplementDynamicProvider()
    {
        var action = new StaticAction(ActionDeps);

        (action is IDynamicOutputSchemaProvider).ShouldBeFalse();
    }

    [Fact]
    public void DynamicAction_ImplementsDynamicProvider()
    {
        var action = new DynamicAction(ActionDeps);

        (action is IDynamicOutputSchemaProvider).ShouldBeTrue();
    }

    [Fact]
    public void StaticAction_GetOutputSchema_ReturnsCLRBasedSchema()
    {
        var action = new StaticAction(ActionDeps);

        var schema = action.GetOutputSchema();

        schema.ShouldNotBeNull();
        var properties = schema.GetKeyword<PropertiesKeyword>()?.Properties;
        properties.ShouldNotBeNull();
        properties.Keys.ShouldContain("name");
    }

    [Fact]
    public async Task DynamicAction_GetOutputSchemaAsync_ReturnsSettingsDependentSchema()
    {
        var action = new DynamicAction(ActionDeps);
        var provider = (IDynamicOutputSchemaProvider)action;

        var settings = new Dictionary<string, object?> { ["schemaType"] = "detailed" };
        var schema = await provider.GetOutputSchemaAsync(settings);

        schema.ShouldNotBeNull();
        var properties = schema.GetKeyword<PropertiesKeyword>()?.Properties;
        properties.ShouldNotBeNull();
        properties.Keys.ShouldContain("detailedField");
    }

    [Fact]
    public async Task DynamicAction_GetOutputSchemaAsync_NullSettings_ReturnsNull()
    {
        var action = new DynamicAction(ActionDeps);
        var provider = (IDynamicOutputSchemaProvider)action;

        var schema = await provider.GetOutputSchemaAsync(null);

        schema.ShouldBeNull();
    }

    [Fact]
    public async Task DynamicAction_GetOutputSchemaAsync_EmptySettings_ReturnsNull()
    {
        var action = new DynamicAction(ActionDeps);
        var provider = (IDynamicOutputSchemaProvider)action;

        var schema = await provider.GetOutputSchemaAsync([]);

        schema.ShouldBeNull();
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
            => Task.FromResult(Success(new StaticOutput()));
    }

    private class DynamicSettings
    {
        public string? SchemaType { get; set; }
    }

    [Action("test.dynamic", "Dynamic Action")]
    private class DynamicAction(ActionInfrastructure infrastructure)
        : ActionBase<DynamicSettings, object>(infrastructure), IDynamicOutputSchemaProvider
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        Task<JsonSchema?> IDynamicOutputSchemaProvider.GetOutputSchemaAsync(
            Dictionary<string, object?>? settings, CancellationToken cancellationToken)
        {
            var typed = settings is { Count: > 0 } ? ResolveSettings(settings) : null;
            if (typed?.SchemaType is null)
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
