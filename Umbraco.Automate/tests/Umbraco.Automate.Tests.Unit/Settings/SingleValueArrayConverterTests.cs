using System.Text.Json;
using Shouldly;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Tests.Unit.Settings;

/// <summary>
/// Covers the array-to-scalar deserialization shim required because Umbraco's Dropdown
/// property editor always persists its value as an array, even in single-select mode.
/// Without these, settings POCOs with <c>string</c>/<c>int</c>/enum fields would throw
/// when bound to JSON like <c>{ "field": ["Value"] }</c>.
/// </summary>
public class SingleValueArrayConverterTests
{
    [Fact]
    public void Deserialize_StringField_FromSingleElementArray_UnwrapsToScalar()
    {
        var json = """{"value": ["SkipOnCycle"]}""";
        var result = JsonSerializer.Deserialize<StringHolder>(json, JsonOptions.Settings);
        result!.Value.ShouldBe("SkipOnCycle");
    }

    [Fact]
    public void Deserialize_StringField_FromRawScalar_StillWorks()
    {
        var json = """{"value": "SkipOnCycle"}""";
        var result = JsonSerializer.Deserialize<StringHolder>(json, JsonOptions.Settings);
        result!.Value.ShouldBe("SkipOnCycle");
    }

    [Fact]
    public void Deserialize_StringField_FromEmptyArray_IsNull()
    {
        // Models an unselected Dropdown — the editor sends [] when nothing is chosen.
        var json = """{"value": []}""";
        var result = JsonSerializer.Deserialize<StringHolder>(json, JsonOptions.Settings);
        result!.Value.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_StringField_FromMultiElementArray_Throws()
    {
        // Single-select dropdowns must yield at most one item; multiple is a schema bug
        // we don't want to silently hide.
        var json = """{"value": ["A", "B"]}""";
        Should.Throw<JsonException>(() =>
            JsonSerializer.Deserialize<StringHolder>(json, JsonOptions.Settings));
    }

    [Fact]
    public void Deserialize_IntField_FromSingleElementArray_UnwrapsToScalar()
    {
        var json = """{"value": [42]}""";
        var result = JsonSerializer.Deserialize<IntHolder>(json, JsonOptions.Settings);
        result!.Value.ShouldBe(42);
    }

    [Fact]
    public void Deserialize_BoolField_FromSingleElementArray_UnwrapsToScalar()
    {
        var json = """{"value": [true]}""";
        var result = JsonSerializer.Deserialize<BoolHolder>(json, JsonOptions.Settings);
        result!.Value.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_EnumField_FromSingleElementArray_UnwrapsToScalar()
    {
        // Combined with the existing JsonStringEnumConverter — confirms the two work together.
        var json = """{"value": ["Two"]}""";
        var result = JsonSerializer.Deserialize<EnumHolder>(json, JsonOptions.Settings);
        result!.Value.ShouldBe(Sample.Two);
    }

    [Fact]
    public void Deserialize_StringArrayField_PreservesArray()
    {
        // Genuine multi-value field — the factory must not flatten it.
        var json = """{"values": ["a", "b", "c"]}""";
        var result = JsonSerializer.Deserialize<StringArrayHolder>(json, JsonOptions.Settings);
        result!.Values.ShouldBe(["a", "b", "c"]);
    }

    [Fact]
    public void Deserialize_NestedObject_AppliesUnwrappingPerField()
    {
        // Both fields use the same options; both should unwrap independently.
        var json = """{"name": ["alpha"], "count": [3]}""";
        var result = JsonSerializer.Deserialize<MixedHolder>(json, JsonOptions.Settings);
        result!.Name.ShouldBe("alpha");
        result.Count.ShouldBe(3);
    }

    [Fact]
    public void Serialize_StringField_RoundTripsAsScalar()
    {
        // Output direction must produce a plain scalar, not an array — otherwise we'd
        // pollute storage with the array shape on every save.
        var holder = new StringHolder { Value = "hello" };
        var json = JsonSerializer.Serialize(holder, JsonOptions.Settings);
        json.ShouldBe("""{"value":"hello"}""");
    }

    private sealed class StringHolder { public string? Value { get; set; } }
    private sealed class IntHolder { public int Value { get; set; } }
    private sealed class BoolHolder { public bool Value { get; set; } }
    private sealed class EnumHolder { public Sample Value { get; set; } }
    private sealed class StringArrayHolder { public string[] Values { get; set; } = []; }
    private sealed class MixedHolder { public string? Name { get; set; } public int Count { get; set; } }

    public enum Sample { One, Two, Three }
}
