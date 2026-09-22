using System.Text.Json;
using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Web.Api.Mcp;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Mcp;

public sealed class McpToolArgumentBinderTests
{
    private static readonly List<McpToolInputField> Fields =
    [
        new() { Name = "customerEmail", Type = McpToolInputFieldType.Text, Required = true },
        new() { Name = "amount", Type = McpToolInputFieldType.Number, Required = false },
    ];

    private static IDictionary<string, JsonElement> Args(string json)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public void TryBind_AllPresentAndValid_ReturnsTrue()
    {
        var ok = McpToolArgumentBinder.TryBind(
            Fields, Args("""{"customerEmail":"a@b.com","amount":5}"""), out var bound, out var error);

        ok.ShouldBeTrue();
        error.ShouldBeNull();
        bound["customerEmail"].ShouldBe("a@b.com");
        bound["amount"].ShouldBe(5d);
    }

    [Fact]
    public void TryBind_MissingRequiredField_ReturnsFalse()
    {
        var ok = McpToolArgumentBinder.TryBind(Fields, Args("{}"), out _, out var error);

        ok.ShouldBeFalse();
        error.ShouldContain("customerEmail");
    }

    [Fact]
    public void TryBind_WrongType_ReturnsFalse()
    {
        var ok = McpToolArgumentBinder.TryBind(
            Fields, Args("""{"customerEmail":"a@b.com","amount":"not a number"}"""), out _, out var error);

        ok.ShouldBeFalse();
        error.ShouldContain("amount");
    }

    [Fact]
    public void TryBind_NullArguments_TreatsAsEmpty()
    {
        var ok = McpToolArgumentBinder.TryBind(
            [new() { Name = "optional", Type = McpToolInputFieldType.Text, Required = false }],
            null, out var bound, out var error);

        ok.ShouldBeTrue();
        error.ShouldBeNull();
        bound.ShouldBeEmpty();
    }
}
