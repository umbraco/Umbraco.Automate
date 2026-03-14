using Shouldly;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Bindings.Filters;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Bindings;

public class SettingsBindingResolverTests
{
    private readonly SettingsBindingResolver _resolver = new(
        new BindingEvaluator([new UppercaseFilter()]));

    private readonly Dictionary<string, object?> _data = new()
    {
        ["trigger"] = new Dictionary<string, object?>
        {
            ["name"] = "Hello World",
            ["key"] = "abc-123",
        },
    };

    [Fact]
    public void ResolveBindings_ResolvesMarkedProperty()
    {
        var settings = new MarkedSettings { Message = "Published: ${ trigger.name }" };

        _resolver.ResolveBindings(settings, _data);

        settings.Message.ShouldBe("Published: Hello World");
    }

    [Fact]
    public void ResolveBindings_SkipsUnmarkedProperty()
    {
        var settings = new MixedSettings
        {
            Marked = "${ trigger.name }",
            Unmarked = "${ trigger.name }",
        };

        _resolver.ResolveBindings(settings, _data);

        settings.Marked.ShouldBe("Hello World");
        settings.Unmarked.ShouldBe("${ trigger.name }");
    }

    [Fact]
    public void ResolveBindings_SkipsNullValues()
    {
        var settings = new MarkedSettings { Message = null! };

        _resolver.ResolveBindings(settings, _data);

        settings.Message.ShouldBeNull();
    }

    [Fact]
    public void ResolveBindings_SkipsEmptyValues()
    {
        var settings = new MarkedSettings { Message = string.Empty };

        _resolver.ResolveBindings(settings, _data);

        settings.Message.ShouldBe(string.Empty);
    }

    [Fact]
    public void ResolveBindings_SkipsNonStringProperties()
    {
        var settings = new NonStringSettings { Count = 42 };

        _resolver.ResolveBindings(settings, _data);

        settings.Count.ShouldBe(42);
    }

    [Fact]
    public void ResolveBindings_HandlesMultipleBindingsInOneField()
    {
        var settings = new MarkedSettings { Message = "${ trigger.name } - ${ trigger.key }" };

        _resolver.ResolveBindings(settings, _data);

        settings.Message.ShouldBe("Hello World - abc-123");
    }

    [Fact]
    public void ResolveBindings_SkipsPropertiesWithoutFieldAttribute()
    {
        var settings = new NoAttributeSettings { Value = "${ trigger.name }" };

        _resolver.ResolveBindings(settings, _data);

        settings.Value.ShouldBe("${ trigger.name }");
    }

    [Fact]
    public void ResolveBindings_WorksWithFilters()
    {
        var settings = new MarkedSettings { Message = "${ trigger.name | uppercase }" };

        _resolver.ResolveBindings(settings, _data);

        settings.Message.ShouldBe("HELLO WORLD");
    }

    // --- Test settings POCOs ---

    private sealed class MarkedSettings
    {
        [Field(SupportsBindings = true)]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class MixedSettings
    {
        [Field(SupportsBindings = true)]
        public string Marked { get; set; } = string.Empty;

        [Field]
        public string Unmarked { get; set; } = string.Empty;
    }

    private sealed class NonStringSettings
    {
        [Field(SupportsBindings = true)]
        public int Count { get; set; }
    }

    private sealed class NoAttributeSettings
    {
        public string Value { get; set; } = string.Empty;
    }
}
