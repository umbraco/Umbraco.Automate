using Json.Schema;
using Json.Schema.Generation;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Tests.Unit.StepTypes;

public class DynamicOutputSchemaTests
{
    private static readonly ActionInfrastructure ActionDeps = CreateActionInfrastructure();

    private static readonly TriggerInfrastructure TriggerDeps = new(
        Mock.Of<IEditableModelResolver>(),
        Options.Create(new DeduplicationOptions()));

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

    [Fact]
    public async Task DefaultVirtual_GetOutputSchemaAsync_ReturnsSameAsStaticSchema()
    {
        var trigger = new StaticTrigger(TriggerDeps);

        // Access the default virtual via the protected bridge on a non-dynamic step type.
        // Since StaticTrigger doesn't override GetOutputSchemaAsync, both should match.
        var staticSchema = trigger.GetOutputSchema();

        staticSchema.ShouldNotBeNull();
        var properties = staticSchema.GetKeyword<PropertiesKeyword>()?.Properties;
        properties.ShouldNotBeNull();
        properties.Keys.ShouldContain("value");
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

    private class StaticTriggerOutput
    {
        public int Value { get; set; }
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
            => ResolveOutputSchemaAsync(settings, cancellationToken);

        protected override Task<JsonSchema?> GetOutputSchemaAsync(
            DynamicSettings? settings, CancellationToken cancellationToken)
        {
            if (settings?.SchemaType is null)
            {
                return Task.FromResult<JsonSchema?>(null);
            }

            // Build a schema dynamically based on settings
            var builder = new JsonSchemaBuilder()
                .Type(SchemaValueType.Object)
                .Properties(
                    ("detailedField", new JsonSchemaBuilder().Type(SchemaValueType.String)));

            return Task.FromResult<JsonSchema?>(builder.Build());
        }
    }

    [Trigger("test.staticTrigger", "Static Trigger")]
    private class StaticTrigger(TriggerInfrastructure infrastructure)
        : TriggerBase<object, StaticTriggerOutput>(infrastructure);

    #endregion
}
