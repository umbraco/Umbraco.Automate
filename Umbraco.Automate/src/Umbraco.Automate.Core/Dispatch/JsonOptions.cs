using System.Text.Json;

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
    /// </summary>
    public static readonly JsonSerializerOptions Settings = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    /// <summary>
    /// Converts a <see cref="JsonElement"/> to a plain .NET primitive so it survives
    /// the Newtonsoft.Json round-trip used by the WorkflowCore persistence layer.
    /// </summary>
    public static object? UnwrapJsonElement(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        // Arrays and objects: preserve as raw JSON string for downstream consumers.
        _ => element.GetRawText(),
    };

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
