using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ManualTriggerTests
{
    private readonly ManualTrigger _trigger = new();

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.manual");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Manual Trigger");

    [Fact]
    public void HasNoSettings()
        => _trigger.SettingsType.ShouldBeNull();

    [Fact]
    public void HasNoOutput()
        => _trigger.OutputType.ShouldBeNull();

    [Fact]
    public void HasNoSettingsSchema()
        => _trigger.GetSettingsSchema().ShouldBeNull();

    [Fact]
    public void HasNoOutputProperties()
        => _trigger.GetOutputProperties().ShouldBeEmpty();
}
