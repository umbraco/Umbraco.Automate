using System.Text.Json;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// Builds the JSON Schema an <see cref="McpTrigger"/>'s declared input fields are advertised as
/// to an AI agent, matching the shape <see cref="ModelContextProtocol.Protocol.Tool.InputSchema"/> requires.
/// </summary>
internal static class McpToolSchemaBuilder
{
    public static JsonElement BuildInputSchema(IReadOnlyList<McpToolInputField> fields)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var field in fields)
        {
            var property = new Dictionary<string, object?>
            {
                ["type"] = ToJsonSchemaType(field.Type),
            };

            if (!string.IsNullOrEmpty(field.Description))
            {
                property["description"] = field.Description;
            }

            properties[field.Name] = property;

            if (field.Required)
            {
                required.Add(field.Name);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static string ToJsonSchemaType(McpToolInputFieldType type) => type switch
    {
        McpToolInputFieldType.Text => "string",
        McpToolInputFieldType.Number => "number",
        McpToolInputFieldType.Boolean => "boolean",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown MCP input field type."),
    };
}
