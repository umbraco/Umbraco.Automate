using System.Text.Json;
using Json.Schema;
using Json.Schema.Generation;
using Shouldly;
using Umbraco.Automate.Core.Actions.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.StepTypes;

public class OutputSchemaSerializationTests
{
    private static readonly JsonSerializerOptions SchemaOptions = new()
    {
        Converters = { new SchemaJsonConverter() },
    };

    // Test-only output shape covering the three cases the binding picker cares about:
    // a described property, an undescribed one, and an enum-typed one.
    private enum SampleOutcome
    {
        Approved,
        Rejected,
    }

    private sealed class SampleOutputWithDescriptions
    {
        [Description("A described property — this text should reach the schema's description keyword.")]
        public string? DescribedProperty { get; init; }

        public string? UndescribedProperty { get; init; }

        [Description("The outcome of the sample decision.")]
        public SampleOutcome Outcome { get; init; }
    }

    private static JsonElement GetPropertiesElement<T>()
    {
        var config = new SchemaGeneratorConfiguration
        {
            PropertyNameResolver = PropertyNameResolvers.CamelCase,
        };

        var schema = new JsonSchemaBuilder().FromType<T>(config).Build();
        var json = JsonSerializer.Serialize(schema, SchemaOptions);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.TryGetProperty("properties", out var props).ShouldBeTrue();
        return props;
    }

    [Fact]
    public void DescribedProperty_SurfacesDescriptionInSchema()
    {
        var props = GetPropertiesElement<SampleOutputWithDescriptions>();

        props.TryGetProperty("describedProperty", out var described).ShouldBeTrue();
        described.TryGetProperty("description", out var description).ShouldBeTrue();
        description.GetString().ShouldBe("A described property — this text should reach the schema's description keyword.");
    }

    [Fact]
    public void UndescribedProperty_HasNoDescriptionKey()
    {
        var props = GetPropertiesElement<SampleOutputWithDescriptions>();

        props.TryGetProperty("undescribedProperty", out var undescribed).ShouldBeTrue();

        // Absent, not an empty string — the binding picker must be able to tell "no
        // description was given" apart from "the author wrote an empty one".
        undescribed.TryGetProperty("description", out _).ShouldBeFalse();
    }

    [Fact]
    public void EnumProperty_SurfacesAllowedValues()
    {
        var props = GetPropertiesElement<SampleOutputWithDescriptions>();

        props.TryGetProperty("outcome", out var outcome).ShouldBeTrue();
        outcome.TryGetProperty("enum", out var enumValues).ShouldBeTrue();
        enumValues.ValueKind.ShouldBe(JsonValueKind.Array);

        var values = enumValues.EnumerateArray().Select(e => e.GetString()).ToArray();
        values.ShouldContain("Approved");
        values.ShouldContain("Rejected");

        // The description is retained alongside the enum's allowed values.
        outcome.TryGetProperty("description", out var description).ShouldBeTrue();
        description.GetString().ShouldBe("The outcome of the sample decision.");
    }

    [Fact]
    public void BuiltInOutput_StatusCode_HasDescription()
    {
        // A spot check that the built-in output types actually carry [Description],
        // not just that the mechanism works for a synthetic type.
        var props = GetPropertiesElement<HttpRequestOutput>();

        props.TryGetProperty("statusCode", out var statusCode).ShouldBeTrue();
        statusCode.TryGetProperty("description", out var description).ShouldBeTrue();
        description.GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void HttpRequestOutput_ProducesValidJsonSchema()
    {
        var config = new SchemaGeneratorConfiguration
        {
            PropertyNameResolver = PropertyNameResolvers.CamelCase,
        };

        var schema = new JsonSchemaBuilder().FromType<HttpRequestOutput>(config).Build();
        var json = JsonSerializer.Serialize(schema, SchemaOptions);
        var doc = JsonDocument.Parse(json);

        // Should be a JSON Schema object with properties
        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        doc.RootElement.TryGetProperty("type", out var typeElement).ShouldBeTrue();
        typeElement.GetString().ShouldBe("object");

        doc.RootElement.TryGetProperty("properties", out var props).ShouldBeTrue();
        props.TryGetProperty("statusCode", out _).ShouldBeTrue();
        props.TryGetProperty("responseBody", out _).ShouldBeTrue();
        props.TryGetProperty("isSuccess", out _).ShouldBeTrue();
    }

    [Fact]
    public void LogMessageOutput_ProducesValidJsonSchema()
    {
        var config = new SchemaGeneratorConfiguration
        {
            PropertyNameResolver = PropertyNameResolvers.CamelCase,
        };

        var schema = new JsonSchemaBuilder().FromType<LogMessageOutput>(config).Build();
        var json = JsonSerializer.Serialize(schema, SchemaOptions);
        var doc = JsonDocument.Parse(json);

        doc.RootElement.ValueKind.ShouldBe(JsonValueKind.Object);
        doc.RootElement.TryGetProperty("properties", out var props).ShouldBeTrue();
        props.TryGetProperty("message", out var msgProp).ShouldBeTrue();

        // message should be type string
        msgProp.TryGetProperty("type", out var msgType).ShouldBeTrue();
        msgType.GetString().ShouldBe("string");
    }

    [Fact]
    public void JsonDocument_SerializesCleanlyForApiResponse()
    {
        // Simulates what the mapper does: schema → JSON string → JsonDocument
        var config = new SchemaGeneratorConfiguration
        {
            PropertyNameResolver = PropertyNameResolvers.CamelCase,
        };

        var schema = new JsonSchemaBuilder().FromType<HttpRequestOutput>(config).Build();
        var json = JsonSerializer.Serialize(schema, SchemaOptions);
        var doc = JsonDocument.Parse(json);

        // Now serialize the JsonDocument as ASP.NET would in the response
        var apiJson = JsonSerializer.Serialize(doc);

        // Should produce a clean JSON Schema, not C# object properties
        apiJson.ShouldContain("\"properties\"");
        apiJson.ShouldContain("\"statusCode\"");
        apiJson.ShouldNotContain("\"options\"");
        apiJson.ShouldNotContain("\"parent\"");
        apiJson.ShouldNotContain("\"root\"");
    }
}
