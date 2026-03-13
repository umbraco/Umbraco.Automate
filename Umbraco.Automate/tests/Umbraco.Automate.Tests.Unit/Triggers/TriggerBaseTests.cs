using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Tests.Unit.Triggers;

public class TriggerBaseTests
{
    private static readonly TriggerInfrastructure Dependencies = new(
        Mock.Of<IEditableModelResolver>(),
        Options.Create(new DeduplicationOptions()));

    [Fact]
    public void ReadsMetadataFromAttribute()
    {
        var trigger = new TestTrigger(Dependencies);

        trigger.Alias.ShouldBe("testTrigger");
        trigger.Name.ShouldBe("Test Trigger");
        trigger.Description.ShouldBe("A test trigger");
        trigger.Group.ShouldBe("Testing");
        trigger.Icon.ShouldBe("icon-test");
    }

    [Fact]
    public void SettingsType_ReturnsType_WhenNotObject()
    {
        var trigger = new TestTrigger(Dependencies);

        trigger.SettingsType.ShouldBe(typeof(TestSettings));
    }

    [Fact]
    public void SettingsType_ReturnsNull_WhenObject()
    {
        var trigger = new NoSettingsTrigger(Dependencies);

        trigger.SettingsType.ShouldBeNull();
    }

    [Fact]
    public void OutputType_ReturnsType_WhenNotObject()
    {
        var trigger = new TestTrigger(Dependencies);

        trigger.OutputType.ShouldBe(typeof(TestOutput));
    }

    [Fact]
    public void GetOutputProperties_ReturnsCamelCasedNames()
    {
        var trigger = new TestTrigger(Dependencies);
        var props = trigger.GetOutputProperties();

        props.Count.ShouldBe(2);
        props[0].Name.ShouldBe("contentName");
        props[1].Name.ShouldBe("contentKey");
    }

    [Fact]
    public void GetSettingsSchema_ReturnsSchema()
    {
        var trigger = new TestTrigger(Dependencies);
        var schema = trigger.GetSettingsSchema();

        schema.ShouldNotBeNull();
        schema.Fields.Count.ShouldBe(1);
        schema.Fields[0].Label.ShouldBe("Content Type Filter");
        schema.Fields[0].IsSensitive.ShouldBeFalse();
    }

    [Fact]
    public void ThrowsWhenAttributeMissing()
    {
        Should.Throw<InvalidOperationException>(() => new MissingAttributeTrigger(Dependencies));
    }

    [Trigger("testTrigger", "Test Trigger", Description = "A test trigger", Group = "Testing", Icon = "icon-test")]
    private class TestTrigger(TriggerInfrastructure infrastructure) : TriggerBase<TestSettings, TestOutput>(infrastructure);

    [Trigger("noSettings", "No Settings")]
    private class NoSettingsTrigger(TriggerInfrastructure infrastructure) : TriggerBase<object, object>(infrastructure);

    private class MissingAttributeTrigger(TriggerInfrastructure infrastructure) : TriggerBase<object, object>(infrastructure);

    private class TestSettings
    {
        [Field(Label = "Content Type Filter")]
        public string? ContentTypeAlias { get; set; }
    }

    private class TestOutput
    {
        public string ContentName { get; set; } = string.Empty;
        public Guid ContentKey { get; set; }
    }
}
