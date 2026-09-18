using System.Text.Json;
using Shouldly;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

/// <summary>
/// Covers the read-time migration of the HTTP Request action's headers from the JSON object
/// string they were saved as before the key/value editor. Steps saved by an older version are
/// never rewritten in the database, so every run has to understand the old shape.
/// </summary>
public class HttpRequestKeyValueListJsonConverterTests
{
    [Fact]
    public void Deserialize_FromLegacyJsonObjectString_ProducesRows()
    {
        var json = """{"headers": "{\"Authorization\":\"Bearer x\",\"X-Api-Key\":\"key\"}"}""";

        var result = JsonSerializer.Deserialize<HttpRequestSettings>(json, JsonOptions.Settings);

        result!.Headers.Count.ShouldBe(2);
        result.Headers[0].Key.ShouldBe("Authorization");
        result.Headers[0].Value.ShouldBe("Bearer x");
        result.Headers[1].Key.ShouldBe("X-Api-Key");
        result.Headers[1].Value.ShouldBe("key");
    }

    [Fact]
    public void Deserialize_FromRowArray_ProducesRows()
    {
        var json = """{"headers": [{"key": "Authorization", "value": "Bearer x"}]}""";

        var result = JsonSerializer.Deserialize<HttpRequestSettings>(json, JsonOptions.Settings);

        result!.Headers.Count.ShouldBe(1);
        result.Headers[0].Key.ShouldBe("Authorization");
        result.Headers[0].Value.ShouldBe("Bearer x");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Deserialize_FromBlankLegacyString_ProducesNoRows(string legacy)
    {
        var json = $$"""{"headers": "{{legacy}}"}""";

        var result = JsonSerializer.Deserialize<HttpRequestSettings>(json, JsonOptions.Settings);

        result!.Headers.ShouldBeEmpty();
    }

    [Fact]
    public void Deserialize_FromJsonObject_ProducesRows()
    {
        // Not a shape we save, but the same intent expressed without the string wrapper.
        var json = """{"headers": {"Authorization": "Bearer x"}}""";

        var result = JsonSerializer.Deserialize<HttpRequestSettings>(json, JsonOptions.Settings);

        result!.Headers.Count.ShouldBe(1);
        result.Headers[0].Key.ShouldBe("Authorization");
        result.Headers[0].Value.ShouldBe("Bearer x");
    }

    [Fact]
    public void Deserialize_FromLegacyStringWithNonStringValue_KeepsTheValue()
    {
        var json = """{"headers": "{\"X-Retries\":3}"}""";

        var result = JsonSerializer.Deserialize<HttpRequestSettings>(json, JsonOptions.Settings);

        result!.Headers.Single().Value.ShouldBe("3");
    }

    [Fact]
    public void Deserialize_FromMalformedLegacyString_Throws()
    {
        // The whole point of the change: the old action caught the JsonException and sent the
        // request with no headers at all, so a typo silently produced an unauthenticated call.
        var json = """{"headers": "{\"Authorization\": }"}""";

        var act = () => JsonSerializer.Deserialize<HttpRequestSettings>(json, JsonOptions.Settings);

        var exception = act.ShouldThrow<JsonException>();
        exception.Message.ShouldContain("key/value rows");
    }

    [Fact]
    public void Deserialize_FromLegacyStringThatIsNotAnObject_Throws()
    {
        var json = """{"headers": "[1, 2]"}""";

        var act = () => JsonSerializer.Deserialize<HttpRequestSettings>(json, JsonOptions.Settings);

        act.ShouldThrow<JsonException>();
    }

    [Fact]
    public void Deserialize_FromLegacyStringWithStructuredValue_Throws()
    {
        var json = """{"headers": "{\"X-Thing\":{\"nested\":true}}"}""";

        var act = () => JsonSerializer.Deserialize<HttpRequestSettings>(json, JsonOptions.Settings);

        act.ShouldThrow<JsonException>().Message.ShouldContain("X-Thing");
    }

    [Fact]
    public void Serialize_WritesTheRowArrayShape()
    {
        var settings = new HttpRequestSettings
        {
            Headers = [new HttpRequestKeyValue { Key = "Authorization", Value = "Bearer x" }],
        };

        var json = JsonSerializer.Serialize(settings, JsonOptions.Settings);

        json.ShouldContain("""
            "headers":[{"key":"Authorization","value":"Bearer x"}]
            """.Trim());
    }

    [Fact]
    public void SerializeAndDeserialize_RoundTripsRows()
    {
        var settings = new HttpRequestSettings
        {
            BodyMode = HttpRequestBodyMode.Form,
            Headers = [new HttpRequestKeyValue { Key = "Authorization", Value = "Bearer x" }],
            FormFields = [new HttpRequestKeyValue { Key = "grant_type", Value = "client_credentials" }],
        };

        var json = JsonSerializer.Serialize(settings, JsonOptions.Settings);
        var result = JsonSerializer.Deserialize<HttpRequestSettings>(json, JsonOptions.Settings);

        result!.BodyMode.ShouldBe(HttpRequestBodyMode.Form);
        result.Headers.Single().Value.ShouldBe("Bearer x");
        result.FormFields.Single().Key.ShouldBe("grant_type");
    }
}
