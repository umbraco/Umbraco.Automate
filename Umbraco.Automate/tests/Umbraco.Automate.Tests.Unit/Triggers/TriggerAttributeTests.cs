using Shouldly;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Tests.Unit.Triggers;

public class TriggerAttributeTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var attr = new TriggerAttribute("contentPublished", "Content Published");

        attr.Alias.ShouldBe("contentPublished");
        attr.Name.ShouldBe("Content Published");
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        var attr = new TriggerAttribute("test", "Test");

        attr.Description.ShouldBeNull();
        attr.Group.ShouldBeNull();
        attr.Icon.ShouldBeNull();
    }

    [Fact]
    public void OptionalProperties_CanBeSet()
    {
        var attr = new TriggerAttribute("contentPublished", "Content Published")
        {
            Description = "Fires when content is published",
            Group = "Content",
            Icon = "icon-globe",
        };

        attr.Description.ShouldBe("Fires when content is published");
        attr.Group.ShouldBe("Content");
        attr.Icon.ShouldBe("icon-globe");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_ThrowsForInvalidAlias(string? alias)
    {
        Should.Throw<ArgumentException>(() => new TriggerAttribute(alias!, "Name"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_ThrowsForInvalidName(string? name)
    {
        Should.Throw<ArgumentException>(() => new TriggerAttribute("alias", name!));
    }

    [Fact]
    public void Attribute_IsReadableViaReflection()
    {
        var attr = typeof(DecoratedTrigger)
            .GetCustomAttributes(typeof(TriggerAttribute), false)
            .Cast<TriggerAttribute>()
            .Single();

        attr.Alias.ShouldBe("testTrigger");
        attr.Name.ShouldBe("Test Trigger");
        attr.Group.ShouldBe("Testing");
    }

    [Trigger("testTrigger", "Test Trigger", Group = "Testing")]
    private class DecoratedTrigger;
}
