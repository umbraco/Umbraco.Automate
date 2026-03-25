using System.Text.Json;

namespace Umbraco.Automate.Core.Dispatch;

/// <summary>
/// Shared JSON serialization options for dispatch messages.
/// </summary>
internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };
}
