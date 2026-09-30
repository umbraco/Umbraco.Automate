using System.Text.Json;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Parses the optional "Property Values" JSON object shared by the Create Content and Create
/// Media actions into values ready for <c>SetValue</c>.
/// </summary>
internal static class PropertyValuesJson
{
    /// <summary>
    /// Parses a JSON object of property aliases to values. Strings are kept as-is, numbers and
    /// booleans become their CLR equivalents, and objects and arrays are passed on as raw JSON
    /// text (the form block and rich-text style editors store). Null values are skipped.
    /// </summary>
    /// <returns>
    /// The parsed values, or <c>null</c> when the JSON is malformed or not an object.
    /// </returns>
    public static Dictionary<string, object>? TryParse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var values = new Dictionary<string, object>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                var value = Convert(property.Value);
                if (value is not null)
                {
                    values[property.Name] = value;
                }
            }

            return values;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static object? Convert(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => element.TryGetInt64(out var integer) ? integer : element.GetDecimal(),
        JsonValueKind.Object or JsonValueKind.Array => element.GetRawText(),
        _ => null,
    };
}
