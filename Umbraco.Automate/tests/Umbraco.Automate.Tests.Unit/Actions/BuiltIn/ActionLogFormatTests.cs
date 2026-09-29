using Shouldly;
using Umbraco.Automate.Core.Actions.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class ActionLogFormatTests
{
    [Theory]
    [InlineData("https://example.com/api", "https://example.com/api")]
    [InlineData("https://example.com/api?key=secret#frag", "https://example.com/api?…")]
    [InlineData("https://user:pass@example.com:8443/a/b", "https://example.com:8443/a/b")]
    public void Url_StripsUserInfoQueryAndFragment(string url, string expected)
        => ActionLogFormat.Url(new Uri(url)).ShouldBe(expected);

    [Fact]
    public void Url_RelativeOrMissing_IsDescribedAsInvalid()
    {
        ActionLogFormat.Url(null).ShouldBe("(invalid URL)");
        ActionLogFormat.Url(new Uri("/relative?x=1", UriKind.Relative)).ShouldBe("(invalid URL)");
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(3277, "3.2 KB")]
    [InlineData(1468006, "1.4 MB")]
    public void Bytes_UsesTheLargestSensibleUnit(long bytes, string expected)
        => ActionLogFormat.Bytes(bytes).ShouldBe(expected);

    [Theory]
    [InlineData("00:00:00.140", "140 ms")]
    [InlineData("00:00:02.345", "2.3 s")]
    [InlineData("00:01:05", "1 min 5 s")]
    public void Elapsed_FormatsMeasuredTime(string elapsed, string expected)
        => ActionLogFormat.Elapsed(TimeSpan.Parse(elapsed)).ShouldBe(expected);

    [Theory]
    [InlineData("00:05:00", "5 minutes")]
    [InlineData("01:30:00", "1 hour 30 minutes")]
    [InlineData("2.00:00:01", "2 days 1 second")]
    [InlineData("00:00:00.500", "500 ms")]
    public void Duration_FormatsConfiguredTimeInWords(string duration, string expected)
        => ActionLogFormat.Duration(TimeSpan.Parse(duration)).ShouldBe(expected);

    [Fact]
    public void Item_FallsBackToTheKeyWhenTheNameIsUnknown()
    {
        var key = Guid.NewGuid();

        ActionLogFormat.Item("Home", key).ShouldBe($"'Home' ({key})");
        ActionLogFormat.Item(null, key).ShouldBe(key.ToString());
    }
}
