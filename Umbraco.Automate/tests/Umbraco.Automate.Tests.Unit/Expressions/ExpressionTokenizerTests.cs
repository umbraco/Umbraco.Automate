using Shouldly;
using Umbraco.Automate.Core.Expressions;

namespace Umbraco.Automate.Tests.Unit.Expressions;

public class ExpressionTokenizerTests
{
    [Fact]
    public void Tokenize_SimplePath()
    {
        var token = ExpressionTokenizer.Tokenize("trigger.contentName");

        token.Path.ShouldBe("trigger.contentName");
        token.Filters.ShouldBeEmpty();
    }

    [Fact]
    public void Tokenize_PathWithFilter()
    {
        var token = ExpressionTokenizer.Tokenize("trigger.body | truncate:100");

        token.Path.ShouldBe("trigger.body");
        token.Filters.Count.ShouldBe(1);
        token.Filters[0].Alias.ShouldBe("truncate");
        token.Filters[0].Args.ShouldBe(new[] { "100" });
    }

    [Fact]
    public void Tokenize_ChainedFilters()
    {
        var token = ExpressionTokenizer.Tokenize("trigger.body | stripHtml | truncate:200:...");

        token.Path.ShouldBe("trigger.body");
        token.Filters.Count.ShouldBe(2);
        token.Filters[0].Alias.ShouldBe("stripHtml");
        token.Filters[0].Args.ShouldBeEmpty();
        token.Filters[1].Alias.ShouldBe("truncate");
        token.Filters[1].Args.ShouldBe(new[] { "200", "..." });
    }

    [Fact]
    public void FindExpressions_FindsMultiple()
    {
        var template = "Hello ${ trigger.name }, your ID is ${ trigger.id }!";
        var expressions = ExpressionTokenizer.FindExpressions(template).ToList();

        expressions.Count.ShouldBe(2);
        expressions[0].Content.ShouldBe("trigger.name");
        expressions[1].Content.ShouldBe("trigger.id");
    }

    [Fact]
    public void FindExpressions_ReturnsEmpty_ForNoExpressions()
    {
        var expressions = ExpressionTokenizer.FindExpressions("plain text").ToList();

        expressions.ShouldBeEmpty();
    }

    [Fact]
    public void FindExpressions_HandlesUnclosed()
    {
        var expressions = ExpressionTokenizer.FindExpressions("Hello ${ unclosed").ToList();

        expressions.ShouldBeEmpty();
    }
}
