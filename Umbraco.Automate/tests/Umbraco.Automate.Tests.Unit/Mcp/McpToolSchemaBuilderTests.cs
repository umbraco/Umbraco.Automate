using System.Text.Json;
using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Web.Api.Mcp;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Mcp;

public sealed class McpToolSchemaBuilderTests
{
    [Fact]
    public void BuildInputSchema_NoFields_ReturnsEmptyObjectSchema()
    {
        var schema = McpToolSchemaBuilder.BuildInputSchema([]);

        schema.GetProperty("type").GetString().ShouldBe("object");
        schema.GetProperty("properties").EnumerateObject().ShouldBeEmpty();
    }

    [Fact]
    public void BuildInputSchema_MapsFieldTypesAndRequired()
    {
        var fields = new List<McpToolInputField>
        {
            new() { Name = "customerEmail", Type = McpToolInputFieldType.Text, Required = true, Description = "Who to email" },
            new() { Name = "amount", Type = McpToolInputFieldType.Number, Required = false },
            new() { Name = "urgent", Type = McpToolInputFieldType.Boolean, Required = false },
        };

        var schema = McpToolSchemaBuilder.BuildInputSchema(fields);

        var properties = schema.GetProperty("properties");
        properties.GetProperty("customerEmail").GetProperty("type").GetString().ShouldBe("string");
        properties.GetProperty("customerEmail").GetProperty("description").GetString().ShouldBe("Who to email");
        properties.GetProperty("amount").GetProperty("type").GetString().ShouldBe("number");
        properties.GetProperty("urgent").GetProperty("type").GetString().ShouldBe("boolean");

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        required.ShouldBe(["customerEmail"]);
    }
}
