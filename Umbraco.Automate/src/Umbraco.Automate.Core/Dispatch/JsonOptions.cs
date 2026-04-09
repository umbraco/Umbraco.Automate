using System.Text.Json;
using System.Text.Json.Serialization;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Shared JSON serialization options for dispatch messages and settings.
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
    /// Includes string enum converter for settings that contain enum-typed properties
    /// (e.g. ConditionOperator in IfControlFlowSettings).
    /// </summary>
    public static readonly JsonSerializerOptions Settings = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };
}
