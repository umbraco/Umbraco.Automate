using System.Text.Json.Nodes;
using Umbraco.Automate.Core.Actions;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Prepares the log entries an action recorded for storage on its step run: occurrences of the
/// step's sensitive setting values are replaced with <see cref="SensitiveDataMasker.MaskedValue"/>
/// and over-long messages are truncated. Runs before the entries are persisted, so a resolved
/// secret that an action writes into a message never lands in the step run record.
/// </summary>
/// <remarks>
/// Like <see cref="SensitiveDataMasker"/>, this is best-effort defence in depth: only values the
/// masker would mask in the step's settings are redacted, and only where they appear verbatim.
/// </remarks>
internal static class ActionLogSanitizer
{
    /// <summary>
    /// The longest message stored, in characters. Longer messages are cut and end with
    /// <see cref="TruncatedSuffix"/>.
    /// </summary>
    public const int MaxMessageLength = 4_000;

    /// <summary>Appended to a message that was cut to <see cref="MaxMessageLength"/>.</summary>
    public const string TruncatedSuffix = "… [truncated]";

    /// <summary>
    /// Values shorter than this are not redacted: a short sensitive value (a PIN, say) would
    /// otherwise mask unrelated text wherever those characters happen to appear.
    /// </summary>
    private const int MinRedactedValueLength = 4;

    /// <summary>
    /// Collects the string values the masker would hide in a step's serialized settings, by
    /// masking a copy and comparing it with the original. Using the masker itself keeps the
    /// rules (key patterns, sensitive schema fields, key/value rows) identical to those applied
    /// to the recorded step input.
    /// </summary>
    public static IReadOnlyCollection<string> CollectSensitiveValues(JsonNode? settings, IReadOnlySet<string>? sensitiveFieldKeys)
    {
        if (settings is null)
        {
            return [];
        }

        var masked = SensitiveDataMasker.Mask(settings.DeepClone(), sensitiveFieldKeys);
        var values = new HashSet<string>(StringComparer.Ordinal);
        CollectMaskedValues(settings, masked, values);
        return values;
    }

    /// <summary>
    /// Returns the entries with sensitive values redacted and long messages truncated.
    /// </summary>
    public static List<ActionLogEntry> Sanitize(IEnumerable<ActionLogEntry> entries, IReadOnlyCollection<string> sensitiveValues)
    {
        // Longest first, so a value that contains another is redacted whole.
        var redact = sensitiveValues
            .Where(v => v.Length >= MinRedactedValueLength)
            .OrderByDescending(v => v.Length)
            .ToArray();

        return entries.Select(entry => entry with { Message = SanitizeMessage(entry.Message, redact) }).ToList();
    }

    private static string SanitizeMessage(string message, string[] redact)
    {
        foreach (var value in redact)
        {
            message = message.Replace(value, SensitiveDataMasker.MaskedValue, StringComparison.Ordinal);
        }

        if (message.Length <= MaxMessageLength)
        {
            return message;
        }

        return string.Concat(message.AsSpan(0, MaxMessageLength - TruncatedSuffix.Length), TruncatedSuffix);
    }

    private static void CollectMaskedValues(JsonNode? original, JsonNode? masked, HashSet<string> values)
    {
        if (original is null || masked is null)
        {
            return;
        }

        // Replaced wholesale by the mask: everything under the original is sensitive.
        if (masked is JsonValue maskedValue
            && maskedValue.TryGetValue(out string? maskedText)
            && maskedText == SensitiveDataMasker.MaskedValue)
        {
            CollectStrings(original, values);
            return;
        }

        switch (original)
        {
            case JsonObject originalObject when masked is JsonObject maskedObject:
                foreach (var (key, value) in originalObject)
                {
                    CollectMaskedValues(value, maskedObject[key], values);
                }

                break;

            case JsonArray originalArray when masked is JsonArray maskedArray:
                for (var i = 0; i < originalArray.Count && i < maskedArray.Count; i++)
                {
                    CollectMaskedValues(originalArray[i], maskedArray[i], values);
                }

                break;

            // A string holding JSON that the masker parsed, masked inside and re-serialized.
            case JsonValue originalValue
                when originalValue.TryGetValue(out string? originalText)
                    && masked is JsonValue changedValue
                    && changedValue.TryGetValue(out string? changedText)
                    && originalText != changedText
                    && TryParse(originalText) is { } originalJson
                    && TryParse(changedText) is { } changedJson:
                CollectMaskedValues(originalJson, changedJson, values);
                break;
        }
    }

    private static JsonNode? TryParse(string text)
    {
        try
        {
            return JsonNode.Parse(text);
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static void CollectStrings(JsonNode node, HashSet<string> values)
    {
        switch (node)
        {
            case JsonValue value when value.TryGetValue(out string? text) && !string.IsNullOrEmpty(text):
                values.Add(text);
                break;

            case JsonObject obj:
                foreach (var (_, child) in obj)
                {
                    if (child is not null)
                    {
                        CollectStrings(child, values);
                    }
                }

                break;

            case JsonArray array:
                foreach (var child in array)
                {
                    if (child is not null)
                    {
                        CollectStrings(child, values);
                    }
                }

                break;
        }
    }
}
