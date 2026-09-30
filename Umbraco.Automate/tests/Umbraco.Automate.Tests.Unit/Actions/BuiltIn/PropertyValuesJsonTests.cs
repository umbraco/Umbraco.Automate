using Umbraco.Automate.Core.Actions.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class PropertyValuesJsonTests
{
    [Fact]
    public void TryParse_MixedValueKinds_ConvertsEachValue()
    {
        var result = PropertyValuesJson.TryParse(
            """{ "title": "Hi", "count": 3, "price": 1.5, "flag": true, "blocks": { "a": 1 }, "list": [1, 2], "skip": null }""");

        result.ShouldNotBeNull();
        result["title"].ShouldBe("Hi");
        result["count"].ShouldBe(3L);
        result["price"].ShouldBe(1.5m);
        result["flag"].ShouldBe(true);
        result["blocks"].ShouldBe("""{ "a": 1 }""");
        result["list"].ShouldBe("[1, 2]");
        result.ContainsKey("skip").ShouldBeFalse();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2]")]
    [InlineData("\"text\"")]
    public void TryParse_NotAJsonObject_ReturnsNull(string json)
    {
        PropertyValuesJson.TryParse(json).ShouldBeNull();
    }
}
