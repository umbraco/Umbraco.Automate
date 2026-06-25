using Umbraco.Automate.Core.Bindings.Filters;

namespace Umbraco.Automate.Tests.Unit.Bindings.Filters;

public class StripHtmlFilterTests
{
    private static readonly StripHtmlFilter Filter = new();

    [Fact]
    public void StripsTags_FromString()
        => Filter.Apply("<p>Hello <strong>world</strong></p>", []).ShouldBe("Hello world");

    [Fact]
    public void EmptyParagraph_BecomesEmptyString()
        => Filter.Apply("<p></p>", []).ShouldBe(string.Empty);

    [Fact]
    public void PlainString_Unchanged()
        => Filter.Apply("no markup here", []).ShouldBe("no markup here");

    [Fact]
    public void Null_ReturnsNull()
        => Filter.Apply(null, []).ShouldBeNull();
}
