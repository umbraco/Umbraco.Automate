using System.Text.Json;
using Shouldly;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Tests.Unit.Dispatch;

public class JsonOptionsTests
{
    [Fact]
    public void UnwrapJsonElement_Object_ReturnsCaseInsensitiveDictionary()
    {
        var json = """{"Name":"Alice","AGE":30}""";
        using var doc = JsonDocument.Parse(json);

        var result = JsonOptions.UnwrapJsonElement(doc.RootElement);

        var dict = result.ShouldBeOfType<Dictionary<string, object?>>();
        dict["name"].ShouldBe("Alice");
        dict["age"].ShouldBe((long)30);
    }

    [Fact]
    public void UnwrapJsonElement_Array_ReturnsList()
    {
        var json = """["a","b","c"]""";
        using var doc = JsonDocument.Parse(json);

        var result = JsonOptions.UnwrapJsonElement(doc.RootElement);

        var list = result.ShouldBeOfType<List<object?>>();
        list.Count.ShouldBe(3);
        list[0].ShouldBe("a");
        list[2].ShouldBe("c");
    }

    [Fact]
    public void UnwrapJsonElement_NestedObjectInArray_ReturnsNestedStructure()
    {
        var json = """[{"key":"abc","name":"Page One"},{"key":"def","name":"Page Two"}]""";
        using var doc = JsonDocument.Parse(json);

        var result = JsonOptions.UnwrapJsonElement(doc.RootElement);

        var list = result.ShouldBeOfType<List<object?>>();
        list.Count.ShouldBe(2);

        var first = list[0].ShouldBeOfType<Dictionary<string, object?>>();
        first["key"].ShouldBe("abc");
        first["name"].ShouldBe("Page One");
    }

    [Fact]
    public void UnwrapJsonElement_NestedArrayInObject_ReturnsNestedStructure()
    {
        var json = """{"tags":["alpha","beta"],"count":2}""";
        using var doc = JsonDocument.Parse(json);

        var result = JsonOptions.UnwrapJsonElement(doc.RootElement);

        var dict = result.ShouldBeOfType<Dictionary<string, object?>>();
        var tags = dict["tags"].ShouldBeOfType<List<object?>>();
        tags.Count.ShouldBe(2);
        tags[0].ShouldBe("alpha");
        tags[1].ShouldBe("beta");
        dict["count"].ShouldBe((long)2);
    }

    [Fact]
    public void DeserializeToUnwrappedDictionary_NestedObject_ReturnsTraversableDictionary()
    {
        var json = """{"properties":{"subtitle":"Hello","body":"World"},"name":"Test"}""";

        var result = JsonOptions.DeserializeToUnwrappedDictionary(json);

        result["name"].ShouldBe("Test");
        var props = result["properties"].ShouldBeOfType<Dictionary<string, object?>>();
        props["subtitle"].ShouldBe("Hello");
        props["body"].ShouldBe("World");
    }

    [Fact]
    public void DeserializeToUnwrappedDictionary_ArrayProperty_ReturnsList()
    {
        var json = """{"cultures":["en-US","da-DK"],"name":"Page"}""";

        var result = JsonOptions.DeserializeToUnwrappedDictionary(json);

        var cultures = result["cultures"].ShouldBeOfType<List<object?>>();
        cultures.Count.ShouldBe(2);
        cultures[0].ShouldBe("en-US");
        cultures[1].ShouldBe("da-DK");
    }

    [Fact]
    public void DeserializeToUnwrappedDictionary_DeeplyNested_FullyTraversable()
    {
        var json = """{"level1":{"level2":{"level3":"deep"}}}""";

        var result = JsonOptions.DeserializeToUnwrappedDictionary(json);

        var l1 = result["level1"].ShouldBeOfType<Dictionary<string, object?>>();
        var l2 = l1["level2"].ShouldBeOfType<Dictionary<string, object?>>();
        l2["level3"].ShouldBe("deep");
    }
}
