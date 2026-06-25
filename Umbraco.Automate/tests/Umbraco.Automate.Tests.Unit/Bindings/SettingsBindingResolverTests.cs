using Shouldly;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Bindings.Filters;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Bindings;

public class SettingsBindingResolverTests
{
    private readonly SettingsBindingResolver _resolver = new(
        new BindingEvaluator(new BindingFilterCollection(() => [new UppercaseFilter()])));

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

    [Fact]
    public void ResolveBindings_ResolvesBindingsInListItems()
    {
        var settings = new MarkedListSettings
        {
            Columns = ["HELLO ${ trigger.name }", "no binding here", "${ trigger.key }"],
        };

        _resolver.ResolveBindings(settings, _data);

        settings.Columns.ShouldBe(["HELLO Hello World", "no binding here", "abc-123"]);
    }

    [Fact]
    public void ResolveBindings_SkipsUnmarkedListProperty()
    {
        var settings = new MixedListSettings
        {
            Marked = ["${ trigger.name }"],
            Unmarked = ["${ trigger.name }"],
        };

        _resolver.ResolveBindings(settings, _data);

        settings.Marked.ShouldBe(["Hello World"]);
        settings.Unmarked.ShouldBe(["${ trigger.name }"]);
    }

    [Fact]
    public void ResolveBindings_SkipsNullOrEmptyListItems()
    {
        var settings = new MarkedListSettings { Columns = [null!, string.Empty, "${ trigger.name }"] };

        _resolver.ResolveBindings(settings, _data);

        settings.Columns.ShouldBe([null!, string.Empty, "Hello World"]);
    }

    [Fact]
    public void ResolveBindings_ResolvesBindingsInArrayItems()
    {
        // Proves the fix is generic over any IList<string> — not specific to List<string> —
        // by exercising a plain string[] through the same code path.
        var settings = new MarkedArraySettings
        {
            Columns = ["HELLO ${ trigger.name }", "no binding here", "${ trigger.key }"],
        };

        _resolver.ResolveBindings(settings, _data);

        settings.Columns.ShouldBe(["HELLO Hello World", "no binding here", "abc-123"]);
    }

    [Fact]
    public void ResolveBindings_SkipsReadOnlyList()
    {
        // ReadOnlyCollection<string> implements IList<string> but its indexer setter throws
        // NotSupportedException — the resolver must skip it rather than crash.
        var settings = new ReadOnlyListSettings
        {
            Columns = new System.Collections.ObjectModel.ReadOnlyCollection<string>(
                ["${ trigger.name }", "literal"]),
        };

        var act = () => _resolver.ResolveBindings(settings, _data);

        act.ShouldNotThrow();
        settings.Columns.ShouldBe(["${ trigger.name }", "literal"]);
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

    private sealed class MarkedListSettings
    {
        [Field(SupportsBindings = true)]
        public List<string> Columns { get; set; } = [];
    }

    private sealed class MixedListSettings
    {
        [Field(SupportsBindings = true)]
        public List<string> Marked { get; set; } = [];

        [Field]
        public List<string> Unmarked { get; set; } = [];
    }

    private sealed class MarkedArraySettings
    {
        [Field(SupportsBindings = true)]
        public string[] Columns { get; set; } = [];
    }

    private sealed class ReadOnlyListSettings
    {
        [Field(SupportsBindings = true)]
        public System.Collections.ObjectModel.ReadOnlyCollection<string> Columns { get; set; } =
            new([]);
    }
}
