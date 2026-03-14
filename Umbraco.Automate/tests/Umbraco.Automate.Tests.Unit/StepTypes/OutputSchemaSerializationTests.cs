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
