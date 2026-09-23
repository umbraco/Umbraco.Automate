using System.Text.Json;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Normalizes a nested settings value to a dictionary, whichever shape it arrived in.
/// </summary>
internal static class SettingsDictionary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Converts a nested settings value (a dictionary, a <see cref="JsonElement"/> object or a POCO)
    /// to a case-insensitive dictionary. Returns <c>null</c> for <c>null</c> or non-object values.
    /// </summary>
    public static Dictionary<string, object?>? From(object? value)
    {
        var dict = value switch
        {
            null => null,
            Dictionary<string, object?> d => d,
            JsonElement { ValueKind: JsonValueKind.Object } element
                => JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText(), JsonOptions),
            JsonElement => null,
            string => null,
            _ => JsonSerializer.Deserialize<Dictionary<string, object?>>(
                    JsonSerializer.Serialize(value, JsonOptions), JsonOptions),
        };

        return dict is null ? null : new Dictionary<string, object?>(dict, StringComparer.OrdinalIgnoreCase);
    }
}
