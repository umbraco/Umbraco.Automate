using Shouldly;
using Umbraco.Automate.Core.Bindings;

namespace Umbraco.Automate.Tests.Unit.Bindings;

public class BindingTokenizerTests
{
    [Fact]
    public void Tokenize_SimplePath()
    {
        var token = BindingTokenizer.Tokenize("trigger.contentName");

        token.Path.ShouldBe("trigger.contentName");
        token.Filters.ShouldBeEmpty();
    }

    [Fact]
    public void Tokenize_PathWithFilter()
    {
        var token = BindingTokenizer.Tokenize("trigger.body | truncate:100");

        token.Path.ShouldBe("trigger.body");
        token.Filters.Count.ShouldBe(1);
        token.Filters[0].Alias.ShouldBe("truncate");
        token.Filters[0].Args.ShouldBe(new[] { "100" });
    }

    [Fact]
    public void Tokenize_ChainedFilters()
    {
        var token = BindingTokenizer.Tokenize("trigger.body | stripHtml | truncate:200:...");

        token.Path.ShouldBe("trigger.body");
        token.Filters.Count.ShouldBe(2);
        token.Filters[0].Alias.ShouldBe("stripHtml");
        token.Filters[0].Args.ShouldBeEmpty();
        token.Filters[1].Alias.ShouldBe("truncate");
        token.Filters[1].Args.ShouldBe(new[] { "200", "..." });
    }

    [Fact]
    public void FindBindings_FindsMultiple()
    {
        var template = "Hello ${ trigger.name }, your ID is ${ trigger.id }!";
        var bindings = BindingTokenizer.FindBindings(template).ToList();

        bindings.Count.ShouldBe(2);
        bindings[0].Content.ShouldBe("trigger.name");
        bindings[1].Content.ShouldBe("trigger.id");
    }

    [Fact]
    public void FindBindings_ReturnsEmpty_ForNoBindings()
    {
        var bindings = BindingTokenizer.FindBindings("plain text").ToList();

        bindings.ShouldBeEmpty();
    }

    [Fact]
    public void FindBindings_HandlesUnclosed()
    {
        var bindings = BindingTokenizer.FindBindings("Hello ${ unclosed").ToList();

        bindings.ShouldBeEmpty();
    }
}
