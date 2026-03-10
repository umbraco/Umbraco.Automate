using Shouldly;
using Umbraco.Automate.Core.Expressions;
using Umbraco.Automate.Core.Expressions.Filters;

namespace Umbraco.Automate.Tests.Unit.Expressions;

public class ExpressionEvaluatorTests
{
    private readonly ExpressionEvaluator _evaluator = new(
    [
        new TruncateFilter(),
        new LowercaseFilter(),
        new UppercaseFilter(),
        new FallbackFilter(),
        new StripHtmlFilter(),
    ]);

    private readonly Dictionary<string, object?> _data = new()
    {
        ["trigger"] = new Dictionary<string, object?>
        {
            ["contentName"] = "Hello World",
            ["contentKey"] = Guid.Empty,
            ["body"] = "<p>Some <b>HTML</b> content</p>",
            ["empty"] = null,
        },
        ["steps"] = new Dictionary<string, object?>
        {
            ["sendEmail"] = new Dictionary<string, object?>
            {
                ["messageId"] = "msg-123",
            },
        },
    };

    [Fact]
    public void Evaluate_SimplePath()
    {
        var result = _evaluator.Evaluate("Name: ${ trigger.contentName }", _data);

        result.ShouldBe("Name: Hello World");
    }

    [Fact]
    public void Evaluate_NestedPath()
    {
        var result = _evaluator.Evaluate("ID: ${ steps.sendEmail.messageId }", _data);

        result.ShouldBe("ID: msg-123");
    }

    [Fact]
    public void Evaluate_WithFilter()
    {
        var result = _evaluator.Evaluate("${ trigger.contentName | lowercase }", _data);

        result.ShouldBe("hello world");
    }

    [Fact]
    public void Evaluate_WithChainedFilters()
    {
        var result = _evaluator.Evaluate("${ trigger.body | stripHtml | truncate:10:... }", _data);

        result.ShouldBe("Some HTML ...");
    }

    [Fact]
    public void Evaluate_FallbackOnNull()
    {
        var result = _evaluator.Evaluate("${ trigger.empty | fallback:N/A }", _data);

        result.ShouldBe("N/A");
    }

    [Fact]
    public void Evaluate_MissingPath_ReturnsEmpty()
    {
        var result = _evaluator.Evaluate("${ trigger.nonexistent }", _data);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Evaluate_PlainText_ReturnedUnchanged()
    {
        var result = _evaluator.Evaluate("Just plain text", _data);

        result.ShouldBe("Just plain text");
    }

    [Fact]
    public void Evaluate_MultipleExpressions()
    {
        var result = _evaluator.Evaluate("${ trigger.contentName } (${ steps.sendEmail.messageId })", _data);

        result.ShouldBe("Hello World (msg-123)");
    }

    [Fact]
    public void ResolvePath_HandlesNestedDictionaries()
    {
        var result = ExpressionEvaluator.ResolvePath("steps.sendEmail.messageId", _data);

        result.ShouldBe("msg-123");
    }

    [Fact]
    public void ResolvePath_ReturnsNull_ForMissingKey()
    {
        var result = ExpressionEvaluator.ResolvePath("trigger.missing", _data);

        result.ShouldBeNull();
    }
}
