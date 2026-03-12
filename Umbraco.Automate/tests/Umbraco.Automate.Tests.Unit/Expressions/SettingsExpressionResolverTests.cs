using Shouldly;
using Umbraco.Automate.Core.Expressions;
using Umbraco.Automate.Core.Expressions.Filters;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Expressions;

public class SettingsExpressionResolverTests
{
    private readonly SettingsExpressionResolver _resolver = new(
        new ExpressionEvaluator([new UppercaseFilter()]));

    private readonly Dictionary<string, object?> _data = new()
    {
        ["trigger"] = new Dictionary<string, object?>
        {
            ["name"] = "Hello World",
            ["key"] = "abc-123",
        },
    };

    [Fact]
    public void ResolveExpressions_ResolvesMarkedProperty()
    {
        var settings = new MarkedSettings { Message = "Published: ${ trigger.name }" };

        _resolver.ResolveExpressions(settings, _data);

        settings.Message.ShouldBe("Published: Hello World");
    }

    [Fact]
    public void ResolveExpressions_SkipsUnmarkedProperty()
    {
        var settings = new MixedSettings
        {
            Marked = "${ trigger.name }",
            Unmarked = "${ trigger.name }",
        };

        _resolver.ResolveExpressions(settings, _data);

        settings.Marked.ShouldBe("Hello World");
        settings.Unmarked.ShouldBe("${ trigger.name }");
    }

    [Fact]
    public void ResolveExpressions_SkipsNullValues()
    {
        var settings = new MarkedSettings { Message = null! };

        _resolver.ResolveExpressions(settings, _data);

        settings.Message.ShouldBeNull();
    }

    [Fact]
    public void ResolveExpressions_SkipsEmptyValues()
    {
        var settings = new MarkedSettings { Message = string.Empty };

        _resolver.ResolveExpressions(settings, _data);

        settings.Message.ShouldBe(string.Empty);
    }

    [Fact]
    public void ResolveExpressions_SkipsNonStringProperties()
    {
        var settings = new NonStringSettings { Count = 42 };

        _resolver.ResolveExpressions(settings, _data);

        settings.Count.ShouldBe(42);
    }

    [Fact]
    public void ResolveExpressions_HandlesMultipleExpressionsInOneField()
    {
        var settings = new MarkedSettings { Message = "${ trigger.name } - ${ trigger.key }" };

        _resolver.ResolveExpressions(settings, _data);

        settings.Message.ShouldBe("Hello World - abc-123");
    }

    [Fact]
    public void ResolveExpressions_SkipsPropertiesWithoutFieldAttribute()
    {
        var settings = new NoAttributeSettings { Value = "${ trigger.name }" };

        _resolver.ResolveExpressions(settings, _data);

        settings.Value.ShouldBe("${ trigger.name }");
    }

    [Fact]
    public void ResolveExpressions_WorksWithFilters()
    {
        var settings = new MarkedSettings { Message = "${ trigger.name | uppercase }" };

        _resolver.ResolveExpressions(settings, _data);

        settings.Message.ShouldBe("HELLO WORLD");
    }

    // --- Test settings POCOs ---

    private sealed class MarkedSettings
    {
        [Field(SupportsExpressions = true)]
        public string Message { get; set; } = string.Empty;
    }

    private sealed class MixedSettings
    {
        [Field(SupportsExpressions = true)]
        public string Marked { get; set; } = string.Empty;

        [Field]
        public string Unmarked { get; set; } = string.Empty;
    }

    private sealed class NonStringSettings
    {
        [Field(SupportsExpressions = true)]
        public int Count { get; set; }
    }

    private sealed class NoAttributeSettings
    {
        public string Value { get; set; } = string.Empty;
    }
}
