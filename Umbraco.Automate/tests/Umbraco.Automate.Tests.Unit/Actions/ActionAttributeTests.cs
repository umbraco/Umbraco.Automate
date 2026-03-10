using Shouldly;
using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Tests.Unit.Actions;

public class ActionAttributeTests
{
    [Fact]
    public void Constructor_SetsRequiredProperties()
    {
        var attr = new ActionAttribute("httpRequest", "HTTP Request");

        attr.Alias.ShouldBe("httpRequest");
        attr.Name.ShouldBe("HTTP Request");
    }

    [Fact]
    public void OptionalProperties_DefaultToNull()
    {
        var attr = new ActionAttribute("test", "Test");

        attr.Description.ShouldBeNull();
        attr.Group.ShouldBeNull();
        attr.Icon.ShouldBeNull();
    }

    [Fact]
    public void OptionalProperties_CanBeSet()
    {
        var attr = new ActionAttribute("httpRequest", "HTTP Request")
        {
            Description = "Makes an HTTP request",
            Group = "Core",
            Icon = "icon-link",
        };

        attr.Description.ShouldBe("Makes an HTTP request");
        attr.Group.ShouldBe("Core");
        attr.Icon.ShouldBe("icon-link");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_ThrowsForInvalidAlias(string? alias)
    {
        Should.Throw<ArgumentException>(() => new ActionAttribute(alias!, "Name"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_ThrowsForInvalidName(string? name)
    {
        Should.Throw<ArgumentException>(() => new ActionAttribute("alias", name!));
    }

    [Fact]
    public void Attribute_IsReadableViaReflection()
    {
        var attr = typeof(DecoratedAction)
            .GetCustomAttributes(typeof(ActionAttribute), false)
            .Cast<ActionAttribute>()
            .Single();

        attr.Alias.ShouldBe("testAction");
        attr.Name.ShouldBe("Test Action");
        attr.Icon.ShouldBe("icon-settings");
    }

    [Action("testAction", "Test Action", Icon = "icon-settings")]
    private class DecoratedAction;
}
