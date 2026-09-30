using System.Text.Json.Nodes;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Security;

public class SensitiveDataMaskerTests
{
    private const string Mask = SensitiveDataMasker.MaskedValue;

    [Theory]
    [InlineData("authorization")]
    [InlineData("Authorization")]
    [InlineData("Proxy-Authorization")]
    [InlineData("cookie")]
    [InlineData("Set-Cookie")]
    [InlineData("set_cookie")]
    [InlineData("password")]
    [InlineData("userPassword")]
    [InlineData("passwd")]
    [InlineData("secret")]
    [InlineData("client_secret")]
    [InlineData("clientSecret")]
    [InlineData("token")]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    [InlineData("githubToken")]
    [InlineData("api_key")]
    [InlineData("apikey")]
    [InlineData("api-key")]
    [InlineData("X-Api-Key")]
    [InlineData("private_key")]
    [InlineData("PRIVATE.KEY")]
    public void IsSensitiveKey_SensitiveNames_ReturnsTrue(string key)
        => SensitiveDataMasker.IsSensitiveKey(key).ShouldBeTrue();

    [Theory]
    [InlineData("name")]
    [InlineData("maxTokens")]
    [InlineData("inputTokens")]
    [InlineData("tokenType")]
    [InlineData("token_endpoint")]
    [InlineData("authorizationUrl")]
    [InlineData("passwordHint")]
    [InlineData("cookiesEnabled")]
    [InlineData("")]
    [InlineData(null)]
    public void IsSensitiveKey_OtherNames_ReturnsFalse(string? key)
        => SensitiveDataMasker.IsSensitiveKey(key).ShouldBeFalse();

    [Fact]
    public void Mask_TopLevelSensitiveKeys_AreMasked()
    {
        var node = Parse("""{ "token": "abc", "name": "Rick", "Authorization": "Bearer xyz" }""");

        SensitiveDataMasker.Mask(node);

        node!["token"]!.GetValue<string>().ShouldBe(Mask);
        node["Authorization"]!.GetValue<string>().ShouldBe(Mask);
        node["name"]!.GetValue<string>().ShouldBe("Rick");
    }

    [Fact]
    public void Mask_NestedObjectsAndArrays_AreMaskedAtAnyDepth()
    {
        var node = Parse("""
            {
              "response": {
                "headers": { "Set-Cookie": "session=1", "Content-Type": "application/json" },
                "items": [ { "id": 1, "api_key": "k1" }, { "id": 2, "api_key": "k2" } ]
              }
            }
            """);

        SensitiveDataMasker.Mask(node);

        node!["response"]!["headers"]!["Set-Cookie"]!.GetValue<string>().ShouldBe(Mask);
        node["response"]!["headers"]!["Content-Type"]!.GetValue<string>().ShouldBe("application/json");
        var items = node["response"]!["items"]!.AsArray();
        items[0]!["api_key"]!.GetValue<string>().ShouldBe(Mask);
        items[1]!["api_key"]!.GetValue<string>().ShouldBe(Mask);
        items[0]!["id"]!.GetValue<int>().ShouldBe(1);
    }

    [Fact]
    public void Mask_SensitiveKeyHoldingAnObject_MasksTheWholeValue()
    {
        var node = Parse("""{ "secret": { "value": "abc", "rotated": true } }""");

        SensitiveDataMasker.Mask(node);

        node!["secret"]!.GetValue<string>().ShouldBe(Mask);
    }

    [Fact]
    public void Mask_NullValues_StayNull()
    {
        var node = Parse("""{ "token": null }""");

        SensitiveDataMasker.Mask(node);

        node!["token"].ShouldBeNull();
    }

    [Fact]
    public void Mask_KeyValueRows_MaskValueWhenTheRowKeyIsSensitive()
    {
        var node = Parse("""
            {
              "headers": [
                { "key": "Authorization", "value": "Bearer xyz" },
                { "key": "Accept", "value": "application/json" },
                { "name": "X-Api-Key", "value": "k" }
              ]
            }
            """);

        SensitiveDataMasker.Mask(node);

        var rows = node!["headers"]!.AsArray();
        rows[0]!["value"]!.GetValue<string>().ShouldBe(Mask);
        rows[0]!["key"]!.GetValue<string>().ShouldBe("Authorization");
        rows[1]!["value"]!.GetValue<string>().ShouldBe("application/json");
        rows[2]!["value"]!.GetValue<string>().ShouldBe(Mask);
    }

    [Fact]
    public void Mask_JsonEmbeddedInAString_IsMaskedAndReserialized()
    {
        var node = new JsonObject
        {
            ["statusCode"] = 200,
            ["responseBody"] = """{"access_token":"abc","expires_in":3600}""",
        };

        SensitiveDataMasker.Mask(node);

        var body = JsonNode.Parse(node["responseBody"]!.GetValue<string>())!;
        body["access_token"]!.GetValue<string>().ShouldBe(Mask);
        body["expires_in"]!.GetValue<int>().ShouldBe(3600);
    }

    [Fact]
    public void Mask_JsonEmbeddedInAStringWithNothingSensitive_IsLeftUntouched()
    {
        const string body = """{ "ok" : true }""";
        var node = new JsonObject { ["responseBody"] = body };

        SensitiveDataMasker.Mask(node);

        node["responseBody"]!.GetValue<string>().ShouldBe(body);
    }

    [Fact]
    public void Mask_NonJsonStrings_AreLeftUntouched()
    {
        var node = new JsonObject { ["message"] = "{not json" };

        SensitiveDataMasker.Mask(node);

        node["message"]!.GetValue<string>().ShouldBe("{not json");
    }

    [Fact]
    public void Mask_SchemaSensitiveFieldKeys_AreMaskedAtTopLevelOnly()
    {
        var node = Parse("""{ "webhookUrl": "https://hooks.example/abc", "nested": { "webhookUrl": "keep" } }""");

        SensitiveDataMasker.Mask(node, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "WebhookUrl" });

        node!["webhookUrl"]!.GetValue<string>().ShouldBe(Mask);
        node["nested"]!["webhookUrl"]!.GetValue<string>().ShouldBe("keep");
    }

    [Fact]
    public void Mask_TopLevelArrayOrScalar_DoesNotThrow()
    {
        SensitiveDataMasker.Mask(Parse("""[ { "password": "p" } ]"""))![0]!["password"]!.GetValue<string>().ShouldBe(Mask);
        SensitiveDataMasker.Mask(Parse("\"plain\""))!.GetValue<string>().ShouldBe("plain");
        SensitiveDataMasker.Mask(null).ShouldBeNull();
    }

    [Fact]
    public void GetSensitiveFieldKeys_ReturnsKeyAndPropertyNameOfSensitiveFields()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(SettingsWithSecret));

        var keys = SensitiveDataMasker.GetSensitiveFieldKeys(schema);

        keys.ShouldContain("WebhookUrl");
        keys.ShouldContain("webhookUrl");
        keys.ShouldNotContain("Channel");
    }

    [Fact]
    public void GetSensitiveFieldKeys_NullSchema_ReturnsEmpty()
        => SensitiveDataMasker.GetSensitiveFieldKeys(null).ShouldBeEmpty();

    private static JsonNode? Parse(string json) => JsonNode.Parse(json);

    private sealed class SettingsWithSecret
    {
        [Field(Label = "Webhook URL", IsSensitive = true)]
        public string? WebhookUrl { get; set; }

        [Field(Label = "Channel")]
        public string? Channel { get; set; }
    }
}
