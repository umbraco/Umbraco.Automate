using System.Text.Json;
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
        _serializer = new EditableModelSerializer(_protectorMock.Object);

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
        var model = new TestModel { ApiKey = "$Slack:ApiToken", Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true },
            new EditableModelFieldDescriptor { PropertyName = "Endpoint", Label = "Endpoint", PropertyType = typeof(string), IsSensitive = false });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"apiKey\":\"$Slack:ApiToken\"");
        _protectorMock.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Serialize_WithMixedConfigReferenceAndActualSecret_EncryptsOnlySecret()
    {
        var model = new AwsModel
        {
            AccessKeyId = "$AWS:AccessKeyId",
            SecretAccessKey = "actual-secret-key",
            Region = "us-east-1",
        };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { Key = "accessKeyId", PropertyName = "AccessKeyId", Label = "Access Key ID", PropertyType = typeof(string), IsSensitive = true },
            new EditableModelFieldDescriptor { Key = "secretAccessKey", PropertyName = "SecretAccessKey", Label = "Secret Access Key", PropertyType = typeof(string), IsSensitive = true },
            new EditableModelFieldDescriptor { Key = "region", PropertyName = "Region", Label = "Region", PropertyType = typeof(string), IsSensitive = false });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain("\"accessKeyId\":\"$AWS:AccessKeyId\"");
        result.ShouldContain("\"secretAccessKey\":\"ENC:actual-secret-key\"");
        result.ShouldContain("\"region\":\"us-east-1\"");
        _protectorMock.Verify(p => p.Protect("actual-secret-key"), Times.Once);
        _protectorMock.Verify(p => p.Protect("$AWS:AccessKeyId"), Times.Never);
    }

    [Theory]
    [InlineData("$ConnectionStrings:MyApi")]
    [InlineData("$OpenAI:ApiKey")]
    [InlineData("$Azure:Endpoint")]
    [InlineData("$EnvironmentVariable")]
    public void Serialize_WithVariousConfigReferences_DoesNotEncrypt(string configReference)
    {
        var model = new TestModel { ApiKey = configReference, Endpoint = "https://api.example.com" };
        var schema = CreateSchema(
            new EditableModelFieldDescriptor { PropertyName = "ApiKey", Label = "API Key", PropertyType = typeof(string), IsSensitive = true });

        var result = _serializer.Serialize(model, schema);

        result.ShouldContain($"\"apiKey\":\"{configReference}\"");
        _protectorMock.Verify(p => p.Protect(It.IsAny<string>()), Times.Never);
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
