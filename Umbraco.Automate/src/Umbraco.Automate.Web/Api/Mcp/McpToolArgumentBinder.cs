using System.Text.Json;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// Validates an incoming MCP tool call's arguments against an <see cref="McpTrigger"/>'s declared
/// <see cref="McpToolInputField"/>s and converts them to plain CLR values. Extra arguments not
/// declared on the trigger are ignored rather than rejected.
/// </summary>
internal static class McpToolArgumentBinder
{
    public static bool TryBind(
        IReadOnlyList<McpToolInputField> fields,
        IDictionary<string, JsonElement>? arguments,
        out Dictionary<string, object?> bound,
        out string? error)
    {
        bound = [];
        arguments ??= new Dictionary<string, JsonElement>();

        foreach (var field in fields)
        {
            if (!arguments.TryGetValue(field.Name, out var value))
            {
                if (field.Required)
                {
                    error = $"Missing required argument '{field.Name}'.";
                    return false;
                }

                continue;
            }

            if (!TryConvert(field, value, out var converted))
            {
                error = $"Argument '{field.Name}' must be a {field.Type.ToString().ToLowerInvariant()}.";
                return false;
            }

            bound[field.Name] = converted;
        }

        error = null;
        return true;
    }

    private static bool TryConvert(McpToolInputField field, JsonElement value, out object? converted)
    {
        switch (field.Type)
        {
            case McpToolInputFieldType.Text when value.ValueKind == JsonValueKind.String:
                converted = value.GetString();
                return true;

            case McpToolInputFieldType.Number when value.ValueKind == JsonValueKind.Number:
                converted = value.GetDouble();
                return true;

            case McpToolInputFieldType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                converted = value.GetBoolean();
                return true;

            default:
                converted = null;
                return false;
        }
    }
}
