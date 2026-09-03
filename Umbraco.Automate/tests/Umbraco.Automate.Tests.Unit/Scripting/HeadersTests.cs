using Shouldly;
using Umbraco.Automate.Core.Scripting;

namespace Umbraco.Automate.Tests.Unit.Scripting;

public class HeadersTests
{
    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var headers = new Headers();
        headers.Append("Content-Type", "text/plain");

        headers.Get("content-type").ShouldBe("text/plain");
    }

    [Fact]
    public void Copy_IsCaseInsensitive()
    {
        var source = new Headers();
        source.Append("Content-Type", "text/plain");

        new Headers(source).Get("content-type").ShouldBe("text/plain");
    }

    [Fact]
    public void Copy_AppendDoesNotMutateSource()
    {
        var source = new Headers();
        source.Append("X-Test", "one");

        var copy = new Headers(source);
        copy.Append("X-Test", "two");

        copy.Get("X-Test").ShouldBe("one, two");
        source.Get("X-Test").ShouldBe("one");
    }
}
