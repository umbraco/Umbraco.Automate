using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Testing.Builders;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public sealed class McpTriggerTests
{
    private readonly McpTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
    {
        _trigger.Alias.ShouldBe("umbracoAutomate.mcp");
    }

    [Fact]
    public void HasSettingsAndOutputTypes()
    {
        _trigger.SettingsType.ShouldBe(typeof(McpTriggerSettings));
        _trigger.OutputType.ShouldBe(typeof(McpTriggerOutput));
    }

    [Fact]
    public void CreateManualRunOutput_NoTestArguments_ReturnsEmptyArguments()
    {
        var settings = new McpTriggerSettings();

        var result = _trigger.CreateManualRunOutput(settings);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.ShouldContainKey("arguments");
    }

    [Fact]
    public void CreateManualRunOutput_ValidTestArguments_ReturnsThem()
    {
        var settings = new McpTriggerSettings
        {
            InputFields = [new() { Name = "customerEmail", Type = McpToolInputFieldType.Text }],
            TestArguments = """{ "customerEmail": "a@b.com" }""",
        };

        var result = _trigger.CreateManualRunOutput(settings);

        result.Success.ShouldBeTrue();
        var arguments = result.Data!["arguments"] as Dictionary<string, object?>;
        arguments.ShouldNotBeNull();
        arguments!["customerEmail"].ShouldBe("a@b.com");
    }

    [Fact]
    public void CreateManualRunOutput_InvalidJson_ReturnsInvalid()
    {
        var settings = new McpTriggerSettings { TestArguments = "not json" };

        var result = _trigger.CreateManualRunOutput(settings);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void CreateManualRunOutput_JsonArray_ReturnsInvalid()
    {
        var settings = new McpTriggerSettings { TestArguments = "[1, 2]" };

        var result = _trigger.CreateManualRunOutput(settings);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void CreateManualRunOutput_MissingRequiredArgument_ReturnsInvalidLikeARealCall()
    {
        var settings = new McpTriggerSettings
        {
            InputFields = [new() { Name = "email", Type = McpToolInputFieldType.Text, Required = true }],
            TestArguments = "{}",
        };

        var result = _trigger.CreateManualRunOutput(settings);

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull().ShouldContain("email");
    }

    [Fact]
    public void CreateManualRunOutput_WrongArgumentType_ReturnsInvalidLikeARealCall()
    {
        var settings = new McpTriggerSettings
        {
            InputFields = [new() { Name = "count", Type = McpToolInputFieldType.Number }],
            TestArguments = """{ "count": "three" }""",
        };

        var result = _trigger.CreateManualRunOutput(settings);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public void CreateManualRunOutput_BindsLikeARealCall_DropsUndeclaredAndConvertsNumbers()
    {
        var settings = new McpTriggerSettings
        {
            InputFields = [new() { Name = "count", Type = McpToolInputFieldType.Number }],
            TestArguments = """{ "count": 3, "extra": "ignored" }""",
        };

        var result = _trigger.CreateManualRunOutput(settings);

        result.Success.ShouldBeTrue();
        var arguments = result.Data!["arguments"] as Dictionary<string, object?>;
        arguments.ShouldNotBeNull();
        arguments!.ShouldNotContainKey("extra");
        Convert.ToDouble(arguments["count"]).ShouldBe(3d);
    }

    [Fact]
    public async Task ValidateSettingsForPublishAsync_NoSettings_RequiresAToolName()
    {
        var errors = await _trigger.ValidateSettingsForPublishAsync(null, new AutomationBuilder());

        errors.ShouldHaveSingleItem().ShouldContain("tool name");
    }

    [Fact]
    public async Task ValidateSettingsForPublishAsync_UniqueNamedFields_IsValid()
    {
        var settings = new McpTriggerSettings
        {
            ToolName = "DoThing",
            InputFields = [new() { Name = "a" }, new() { Name = "b" }],
        };

        var errors = await _trigger.ValidateSettingsForPublishAsync(settings, new AutomationBuilder());

        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task ValidateSettingsForPublishAsync_BlankFieldName_ReturnsError()
    {
        var settings = new McpTriggerSettings { ToolName = "DoThing", InputFields = [new() { Name = " " }] };

        var errors = await _trigger.ValidateSettingsForPublishAsync(settings, new AutomationBuilder());

        errors.ShouldHaveSingleItem().ShouldContain("needs a name");
    }

    [Fact]
    public async Task ValidateSettingsForPublishAsync_DuplicateFieldNames_NamesTheDuplicate()
    {
        var settings = new McpTriggerSettings
        {
            ToolName = "DoThing",
            InputFields = [new() { Name = "email" }, new() { Name = "email" }, new() { Name = "name" }],
        };

        var errors = await _trigger.ValidateSettingsForPublishAsync(settings, new AutomationBuilder());

        errors.ShouldHaveSingleItem().ShouldContain("email");
    }
}
