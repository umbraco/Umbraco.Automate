using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
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
        var settings = new McpTriggerSettings { TestArguments = """{ "customerEmail": "a@b.com" }""" };

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
}
