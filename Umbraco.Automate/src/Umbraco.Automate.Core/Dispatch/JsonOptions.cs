using System.Text.Json;
using System.Text.Json.Serialization;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Shared JSON serialization options and utilities for dispatch messages and settings.
/// </summary>
internal static class JsonOptions
{
    /// <summary>
    /// Options for dispatch messages, outbox serialization, and execution data.
    /// Uses strict casing (data is always produced with camelCase by the same serializer).
    /// </summary>
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    /// <summary>
    /// Options for settings deserialization where JSON from the frontend (camelCase)
    /// is mapped to PascalCase POCO models. Case-insensitive to bridge the naming gap.
    /// Includes a string enum converter, plus a factory that flattens single-element
    /// arrays into scalar targets — Umbraco property editors (notably Dropdown) persist
    /// even single selections as arrays, while our settings POCOs declare scalar fields.
    /// </summary>
    public static readonly JsonSerializerOptions Settings = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        // Order matters: SingleValueArrayConverterFactory must come first so it gets
        // first refusal on enum properties — otherwise JsonStringEnumConverter claims
        // them and chokes on the array shape before our flattening can run.
        Converters = { new SingleValueArrayConverterFactory(), new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to a plain .NET type so it survives
    /// the Newtonsoft.Json round-trip used by the WorkflowCore persistence layer.
    /// Objects become case-insensitive dictionaries and arrays become lists,
    /// both recursively, so <see cref="Bindings.BindingEvaluator"/> can traverse
    /// nested paths like <c>steps.GUID.properties.subtitle</c>.
    /// </summary>
    public static object? UnwrapJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.Object => UnwrapJsonObject(element),
        JsonValueKind.Array => UnwrapJsonArray(element),
        _ => element.GetRawText(),
    };

    private static Dictionary<string, object?> UnwrapJsonObject(JsonElement element)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            dict[property.Name] = UnwrapJsonElement(property.Value);
        }

        return dict;
    }

    private static List<object?> UnwrapJsonArray(JsonElement element)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            list.Add(UnwrapJsonElement(item));
        }

        return list;
    }

    /// <summary>
    /// Deserializes a JSON string into a case-insensitive dictionary with unwrapped primitive values.
    /// </summary>
    public static Dictionary<string, object?> DeserializeToUnwrappedDictionary(string json)
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, Default);
        if (raw is null)
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, object?>(raw.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, element) in raw)
        {
            result[key] = UnwrapJsonElement(element);
        }

        return result;
    }
}
