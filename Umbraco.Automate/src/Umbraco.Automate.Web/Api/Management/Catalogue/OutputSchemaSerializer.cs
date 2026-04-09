using System.Text.Json;
using Json.Schema;

namespace Umbraco.Automate.Web.Api.Management.Catalogue;

/// <summary>
/// Serializes a <see cref="JsonSchema"/> to a dictionary representation suitable for API responses.
/// </summary>
internal static class OutputSchemaSerializer
{
    private static readonly JsonSerializerOptions JsonSchemaSerializerOptions = new()
    {
        Converters = { new SchemaJsonConverter() },
    };

    /// <summary>
    /// Serializes a JSON Schema to a dictionary for clean API serialization and Swagger rendering.
    /// </summary>
    /// <param name="schema">The JSON Schema to serialize, or null.</param>
    /// <returns>A dictionary representation of the schema, or null if the input is null.</returns>
    public static Dictionary<string, object?>? Serialize(JsonSchema? schema)
    {
        if (schema is null)
        {
            return null;
        }

        // Serialize via JsonSchema's own converter to get a proper JSON Schema document,
        // then deserialize into a dictionary for clean API serialization and Swagger rendering.
        var json = JsonSerializer.Serialize(schema, JsonSchemaSerializerOptions);
        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
    }
}
