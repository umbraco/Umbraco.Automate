using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Settings;

public class EditableModelSerializerTests
{
    private readonly Mock<ISensitiveFieldProtector> _protectorMock;
    private readonly EditableModelSerializer _serializer;

    public EditableModelSerializerTests()
    {
        _protectorMock = new Mock<ISensitiveFieldProtector>();
        _serializer = new EditableModelSerializer(_protectorMock.Object, CreateConfigReferenceResolver());

        _protectorMock
            .Setup(p => p.Protect(It.IsAny<string?>()))
            .Returns<string?>(v => string.IsNullOrEmpty(v) ? v : $"ENC:{v}");

        _protectorMock
            .Setup(p => p.Unprotect(It.IsAny<string?>()))
            .Returns<string?>(v =>
            {
                if (string.IsNullOrEmpty(v)) return v;
                return v.StartsWith("ENC:") ? v[4..] : v;
            });

        _protectorMock
            .Setup(p => p.IsProtected(It.IsAny<string?>()))
            .Returns<string?>(v => !string.IsNullOrEmpty(v) && v.StartsWith("ENC:"));
    }

    #region Serialize

    [Fact]
    public void Serialize_WithNullModel_ReturnsNull()
    {
        var result = _serializer.Serialize(null, null);

        result.ShouldBeNull();
    }

    [Fact]
    public void Serialize_WithNoSchema_ReturnsJsonWithoutEncryption()
    {
        var model = new TestModel { ApiKey = "secret-key", Endpoint = "https://api.example.com" };

        var result = _serializer.Serialize(model, null);

        result.ShouldNotBeNull();
        result.ShouldContain("\"apiKey\":\"secret-key\"");
        result.ShouldContain("\"endpoint\":\"https://api.example.com\"");
        _protectorMock.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Serialize_WithSchemaHavingNoSensitiveFields_ReturnsJsonWithoutEncryption()
    {
        var model = new TestModel { ApiKey = "secret-key", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = false },
            new EditableModelFieldDescriptor { PropertyName = "Endpoint", Label = "Endpoint", PropertyType = typeof(string), IsSensitive = false });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"apiKey\":\"secret-key\"");
        result.ShouldContain("\"endpoint\":\"https://api.example.com\"");
        _protectorMock.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Serialize_WithSensitiveField_EncryptsOnlySensitiveFields()
    {
        var model = new TestModel { ApiKey = "secret-key", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "apiKey", PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true },
            new EditableModelFieldDescriptor { Key = "endpoint", PropertyName = "Endpoint", Label = "Endpoint", PropertyType = typeof(string), IsSensitive = false });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"apiKey\":\"ENC:secret-key\"");
        result.ShouldContain("\"endpoint\":\"https://api.example.com\"");
        _protectorMock.Verify(p => p.Protect("secret-key"), Times.Once);
    }

    [Fact]
    public void Serialize_WithNullSensitiveField_DoesNotEncrypt()
    {
        var model = new TestModel { ApiKey = null, Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"apiKey\":null");
        _protectorMock.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Serialize_WithConfigurationReference_DoesNotEncrypt()
    {
        var model = new TestModel { ApiKey = "$Umbraco:Automate:Secrets:ApiToken", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "apiKey", PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true },
            new EditableModelFieldDescriptor { Key = "endpoint", PropertyName = "Endpoint", Label = "Endpoint", PropertyType = typeof(string), IsSensitive = false });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"apiKey\":\"$Umbraco:Automate:Secrets:ApiToken\"");
        _protectorMock.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Serialize_WithEmbeddedConfigurationReference_DoesNotEncrypt()
    {
        // Issue #159: a reference embedded in a larger value is a pointer, not a secret, so the
        // whole value is stored plaintext to remain resolvable at read time.
        var model = new TestModel { ApiKey = "Bearer $Umbraco:Automate:Secrets:ApiKey", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "apiKey", PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"apiKey\":\"Bearer $Umbraco:Automate:Secrets:ApiKey\"");
        _protectorMock.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Serialize_WithLiteralDollarSecret_StillEncrypts()
    {
        // A real secret that merely contains a '$' (e.g. a password) is not a configuration
        // reference and must still be encrypted.
        var model = new TestModel { ApiKey = "p$ssw0rd", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "apiKey", PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"apiKey\":\"ENC:p$ssw0rd\"");
        _protectorMock.Verify(p => p.Protect("p$ssw0rd"), Times.Once);
    }

    [Fact]
    public void Serialize_WithReferenceToDisallowedPrefix_Encrypts()
    {
        // Only references under an allowed prefix are treated as configuration pointers. A value
        // that looks like a reference but targets a non-allow-listed section is treated as a
        // literal secret and encrypted (it would never resolve at read time anyway).
        var model = new TestModel { ApiKey = "$Slack:ApiToken", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "apiKey", PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"apiKey\":\"ENC:$Slack:ApiToken\"");
        _protectorMock.Verify(p => p.Protect("$Slack:ApiToken"), Times.Once);
    }

    [Fact]
    public void Serialize_WithEscapedDollarLookalikeReference_Encrypts()
    {
        // A secret whose literal text happens to look like a reference is escaped with "$$".
        // The scanner then sees no allow-listed reference, so the value is treated as a real
        // secret and encrypted rather than stored plaintext as a pointer.
        var model = new TestModel { ApiKey = "$$Umbraco:Automate:Secrets:NotAReference", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "apiKey", PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"apiKey\":\"ENC:$$Umbraco:Automate:Secrets:NotAReference\"");
        _protectorMock.Verify(p => p.Protect("$$Umbraco:Automate:Secrets:NotAReference"), Times.Once);
    }

    [Fact]
    public void Serialize_WithMixedConfigReferenceAndActualSecret_EncryptsOnlySecret()
    {
        var model = new AwsModel
        {
            AccessKeyId = "$Umbraco:Automate:Variables:AccessKeyId",
            SecretAccessKey = "actual-secret-key",
            Region = "us-east-1",
        };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "accessKeyId", PropertyName = "AccessKeyId", Label = "Access Key ID", PropertyType = typeof(string), IsSensitive = true },
            new EditableModelFieldDescriptor { Key = "secretAccessKey", PropertyName = "SecretAccessKey", Label = "Secret Access Key", PropertyType = typeof(string), IsSensitive = true },
            new EditableModelFieldDescriptor { Key = "region", PropertyName = "Region", Label = "Region", PropertyType = typeof(string), IsSensitive = false });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"accessKeyId\":\"$Umbraco:Automate:Variables:AccessKeyId\"");
        result.ShouldContain("\"secretAccessKey\":\"ENC:actual-secret-key\"");
        result.ShouldContain("\"region\":\"us-east-1\"");
        _protectorMock.Verify(p => p.Protect("actual-secret-key"), Times.Once);
        _protectorMock.Verify(p => p.Protect("$Umbraco:Automate:Variables:AccessKeyId"), Times.Never);
    }

    [Theory]
    [InlineData("$Umbraco:Automate:Secrets:MyApi")]
    [InlineData("$Umbraco:Automate:Secrets:OpenAiKey")]
    [InlineData("$Umbraco:Automate:Variables:Endpoint")]
    [InlineData("Bearer $Umbraco:Automate:Secrets:Token")]
    public void Serialize_WithVariousConfigReferences_DoesNotEncrypt(string configReference)
    {
        var model = new TestModel { ApiKey = configReference, Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "apiKey", PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain($"\"apiKey\":\"{configReference}\"");
        _protectorMock.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Serialize_WithEmbeddedReferenceToCustomAllowedPrefix_DoesNotEncrypt()
    {
        // Issue #159: once an admin opts a custom section into AllowedConfigurationKeyPrefixes,
        // an embedded "Bearer $CommunityBlogs:ApiKey" header is a pointer, not a secret, so the
        // serializer stores it plaintext (matching what the resolver will substitute at read time).
        var serializer = CreateSerializer("CommunityBlogs");
        var model = new TestModel { ApiKey = "Bearer $CommunityBlogs:ApiKey", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "apiKey", PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true });

        var result = serializer.Serialize(model, schema);

        result.ShouldContain("\"apiKey\":\"Bearer $CommunityBlogs:ApiKey\"");
        _protectorMock.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Serialize_WithEmbeddedReferenceToNonAllowedCustomPrefix_Encrypts()
    {
        // Without the opt-in, the same value targets a non-allow-listed section, so it is treated
        // as a literal secret and encrypted (it would never resolve at read time anyway). This is
        // the contrast to the test above — the allow-list is what flips the behaviour.
        var model = new TestModel { ApiKey = "Bearer $CommunityBlogs:ApiKey", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "apiKey", PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true });

        var result = _serializer.Serialize(model, schema); // default prefixes — CommunityBlogs not allowed

        result.ShouldContain("\"apiKey\":\"ENC:Bearer $CommunityBlogs:ApiKey\"");
        _protectorMock.Verify(p => p.Protect("Bearer $CommunityBlogs:ApiKey"), Times.Once);
    }

    [Fact]
    public void SerializeAndDeserialize_WithMixedLiteralAndReference_PreservesValue()
    {
        // The mixed literal+reference value must survive a save→load round trip unchanged so
        // the resolver can substitute the reference later.
        var model = new TestModel { ApiKey = "Bearer $Umbraco:Automate:Secrets:ApiKey", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "apiKey", PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true });

        var serialized = _serializer.Serialize(model, schema);
        var deserialized = (JsonElement)_serializer.Deserialize(serialized);

        deserialized.GetProperty("apiKey").GetString().ShouldBe("Bearer $Umbraco:Automate:Secrets:ApiKey");
    }

    #endregion

    #region Deserialize

    [Fact]
    public void Deserialize_WithNullJson_ReturnsDefault()
    {
        var result = (JsonElement)_serializer.Deserialize(null);

        result.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void Deserialize_WithEmptyJson_ReturnsDefault()
    {
        var result = (JsonElement)_serializer.Deserialize(string.Empty);

        result.ValueKind.ShouldBe(JsonValueKind.Undefined);
    }

    [Fact]
    public void Deserialize_WithNoEncryptedFields_ReturnsOriginalValues()
    {
        var json = """{"apiKey":"plain-key","endpoint":"https://api.example.com"}""";

        var result = (JsonElement)_serializer.Deserialize(json);

        result.GetProperty("apiKey").GetString().ShouldBe("plain-key");
        result.GetProperty("endpoint").GetString().ShouldBe("https://api.example.com");
    }

    [Fact]
    public void Deserialize_WithEncryptedField_DecryptsValue()
    {
        var json = """{"apiKey":"ENC:secret-key","endpoint":"https://api.example.com"}""";

        var result = (JsonElement)_serializer.Deserialize(json);

        result.GetProperty("apiKey").GetString().ShouldBe("secret-key");
        result.GetProperty("endpoint").GetString().ShouldBe("https://api.example.com");
        _protectorMock.Verify(p => p.Unprotect("ENC:secret-key"), Times.Once);
    }

    [Fact]
    public void Deserialize_WithNonStringValues_PreservesValues()
    {
        var json = """{"apiKey":"ENC:secret","port":8080,"enabled":true}""";

        var result = (JsonElement)_serializer.Deserialize(json);

        result.GetProperty("apiKey").GetString().ShouldBe("secret");
        result.GetProperty("port").GetInt32().ShouldBe(8080);
        result.GetProperty("enabled").GetBoolean().ShouldBeTrue();
    }

    #endregion

    #region Round Trip

    [Fact]
    public void SerializeAndDeserialize_RoundTrip_PreservesValues()
    {
        var model = new TestModel { ApiKey = "my-secret-api-key", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true },
            new EditableModelFieldDescriptor { PropertyName = "Endpoint", Label = "Endpoint", PropertyType = typeof(string), IsSensitive = false });

        var serialized = _serializer.Serialize(model, schema);
        var deserialized = (JsonElement)_serializer.Deserialize(serialized);

        deserialized.GetProperty("apiKey").GetString().ShouldBe("my-secret-api-key");
        deserialized.GetProperty("endpoint").GetString().ShouldBe("https://api.example.com");
    }

    #endregion

    #region Helpers

    private static EditableModelSchema CreateSchema(params EditableModelFieldDescriptor[] fields)
        => new() { Fields = fields.ToList() };

    // Builds a serializer whose allow-list is exactly the given prefixes, reusing the shared
    // protector mock so encrypt/skip expectations still hold.
    private EditableModelSerializer CreateSerializer(params string[] allowedPrefixes)
        => new(_protectorMock.Object, CreateConfigReferenceResolver(allowedPrefixes));

    // The serializer only asks the service "does this contain a reference?", which depends solely
    // on the allow-list, so an empty configuration suffices here. Passing no prefixes uses the
    // secure defaults (Secrets/Variables), matching the shared _serializer instance.
    private static IConfigurationReferenceResolver CreateConfigReferenceResolver(params string[] allowedPrefixes)
    {
        var automateOptions = allowedPrefixes.Length > 0
            ? new AutomateOptions { AllowedConfigurationKeyPrefixes = allowedPrefixes }
            : new AutomateOptions();
        return new ConfigurationReferenceResolver(
            new ConfigurationBuilder().Build(),
            Options.Create(automateOptions));
    }

    private class TestModel
    {
        public string? ApiKey { get; set; }
        public string? Endpoint { get; set; }
    }

    private class AwsModel
    {
        public string? AccessKeyId { get; set; }
        public string? SecretAccessKey { get; set; }
        public string? Region { get; set; }
    }

    #endregion
}
