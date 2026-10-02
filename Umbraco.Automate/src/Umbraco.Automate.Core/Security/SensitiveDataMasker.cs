using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Replaces secret-looking values in a JSON document with a fixed mask, so run payloads (step
/// input/output, trigger data) can be shown to a user without exposing credentials. Two rules
/// apply, and a value matching either is masked:
/// <list type="bullet">
///   <item>
///     <description>
///       <b>Key pattern</b> — at any depth, a property whose name ends with a sensitive term
///       (<c>authorization</c>, <c>cookie</c>, <c>password</c>, <c>passwd</c>, <c>secret</c>,
///       <c>token</c>, <c>apikey</c>, <c>privatekey</c>), compared case-insensitively with
///       <c>-</c>, <c>_</c>, <c>.</c> and spaces ignored. That covers <c>Set-Cookie</c>,
///       <c>access_token</c>, <c>X-Api-Key</c>, <c>client_secret</c> and so on, while leaving
///       lookalikes such as <c>maxTokens</c>, <c>tokenType</c> or <c>authorizationUrl</c> alone.
///       Key/value rows (<c>{ "key": "Authorization", "value": "…" }</c>, as the HTTP Request
///       headers editor stores them) are matched on the row's <c>key</c>/<c>name</c>.
///     </description>
///   </item>
///   <item>
///     <description>
///       <b>Schema</b> — a top-level property named in <c>sensitiveFieldKeys</c>, i.e. a settings
///       field declared <c>[Field(IsSensitive = true)]</c> (see <see cref="GetSensitiveFieldKeys"/>).
///     </description>
///   </item>
/// </list>
/// String values that themselves hold a JSON object or array (an HTTP response body, say) are
/// parsed and masked too, and re-serialized only when something inside them was masked.
/// </summary>
/// <remarks>
/// Masking is best-effort defence in depth, not a guarantee: a secret stored under an innocuous
/// key, or embedded in free text, is not detected.
/// </remarks>
internal static class SensitiveDataMasker
{
    /// <summary>
    /// The value that replaces a masked value. Matches the mask used for sensitive connection
    /// settings in version comparisons.
    /// </summary>
    public const string MaskedValue = "********";

    /// <summary>
    /// How many levels of JSON-inside-a-string are unpacked. One real level (a response body) is
    /// the common case; the cap stops a crafted payload from recursing indefinitely.
    /// </summary>
    private const int MaxEmbeddedJsonDepth = 3;

    private static readonly string[] SensitiveKeySuffixes =
    [
        "authorization",
        "cookie",
        "password",
        "passwd",
        "secret",
        "token",
        "apikey",
        "privatekey",
    ];

    private static readonly string[] KeyValueRowNameProperties = ["key", "name"];

    /// <summary>
    /// Returns whether a property name matches a sensitive key pattern.
    /// </summary>
    public static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var normalized = Normalize(key);
        foreach (var suffix in SensitiveKeySuffixes)
        {
            if (normalized.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the keys of the fields a settings schema marks sensitive — both the JSON key and the
    /// POCO property name, since a payload may have been serialized either way.
    /// </summary>
    public static IReadOnlySet<string> GetSensitiveFieldKeys(EditableModelSchema? schema)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (schema is null)
        {
            return keys;
        }

        foreach (var field in schema.Fields.Where(f => f.IsSensitive))
        {
            if (!string.IsNullOrEmpty(field.Key))
            {
                keys.Add(field.Key);
            }

            if (!string.IsNullOrEmpty(field.PropertyName))
            {
                keys.Add(field.PropertyName);
            }
        }

        return keys;
    }

    /// <summary>
    /// Masks sensitive values in <paramref name="node"/> in place and returns it.
    /// </summary>
    /// <param name="node">The JSON document to mask. May be <c>null</c>.</param>
    /// <param name="sensitiveFieldKeys">
    /// Top-level property names to mask regardless of the key patterns — typically from
    /// <see cref="GetSensitiveFieldKeys"/>. Compared case-insensitively.
    /// </param>
    public static JsonNode? Mask(JsonNode? node, IReadOnlySet<string>? sensitiveFieldKeys = null)
    {
        if (node is JsonObject obj && sensitiveFieldKeys is { Count: > 0 })
        {
            foreach (var property in obj.ToList())
            {
                if (property.Value is not null && sensitiveFieldKeys.Contains(property.Key))
                {
                    obj[property.Key] = MaskedValue;
                }
            }
        }

        return MaskNode(node, embeddedDepth: 0, out _);
    }

    private static JsonNode? MaskNode(JsonNode? node, int embeddedDepth, out bool changed)
    {
        changed = false;

        switch (node)
        {
            case JsonObject obj:
                changed = MaskObject(obj, embeddedDepth);
                return obj;

            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    var item = array[i];
                    var replacement = MaskNode(item, embeddedDepth, out var itemChanged);
                    if (!ReferenceEquals(replacement, item))
                    {
                        array[i] = replacement;
                    }

                    changed |= itemChanged;
                }

                return array;

            case JsonValue value when embeddedDepth < MaxEmbeddedJsonDepth
                                      && value.TryGetValue<string>(out var text)
                                      && TryMaskEmbeddedJson(text, embeddedDepth, out var masked):
                changed = true;
                return JsonValue.Create(masked);

            default:
                return node;
        }
    }

    private static bool MaskObject(JsonObject obj, int embeddedDepth)
    {
        var changed = false;
        var isSensitiveRow = IsSensitiveKeyValueRow(obj, out var valuePropertyName);

        foreach (var property in obj.ToList())
        {
            if (property.Value is null)
            {
                continue;
            }

            if (IsSensitiveKey(property.Key)
                || (isSensitiveRow && string.Equals(property.Key, valuePropertyName, StringComparison.Ordinal)))
            {
                if (property.Value is not JsonValue existing || existing.ToString() != MaskedValue)
                {
                    obj[property.Key] = MaskedValue;
                    changed = true;
                }

                continue;
            }

            var replacement = MaskNode(property.Value, embeddedDepth, out var childChanged);
            if (!ReferenceEquals(replacement, property.Value))
            {
                obj[property.Key] = replacement;
            }

            changed |= childChanged;
        }

        return changed;
    }

    /// <summary>
    /// Detects a key/value row — an object with a <c>value</c> property and a <c>key</c> or
    /// <c>name</c> property whose string value is a sensitive key (e.g. an HTTP header row).
    /// </summary>
    private static bool IsSensitiveKeyValueRow(JsonObject obj, out string? valuePropertyName)
    {
        valuePropertyName = obj.Select(p => p.Key)
            .FirstOrDefault(k => string.Equals(k, "value", StringComparison.OrdinalIgnoreCase));
        if (valuePropertyName is null)
        {
            return false;
        }

        foreach (var property in obj)
        {
            if (KeyValueRowNameProperties.Contains(property.Key, StringComparer.OrdinalIgnoreCase)
                && property.Value is JsonValue nameValue
                && nameValue.TryGetValue<string>(out var name)
                && IsSensitiveKey(name))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryMaskEmbeddedJson(string text, int embeddedDepth, out string masked)
    {
        masked = text;

        var trimmed = text.AsSpan().Trim();
        if (trimmed.Length < 2
            || !((trimmed[0] == '{' && trimmed[^1] == '}') || (trimmed[0] == '[' && trimmed[^1] == ']')))
        {
            return false;
        }

        JsonNode? parsed;
        try
        {
            parsed = JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return false;
        }

        MaskNode(parsed, embeddedDepth + 1, out var changed);
        if (!changed)
        {
            return false;
        }

        masked = parsed!.ToJsonString();
        return true;
    }

    private static string Normalize(string key)
    {
        Span<char> buffer = key.Length <= 256 ? stackalloc char[key.Length] : new char[key.Length];
        var length = 0;
        foreach (var c in key)
        {
            if (c is '-' or '_' or '.' or ' ')
            {
                continue;
            }

            buffer[length++] = char.ToLowerInvariant(c);
        }

        return new string(buffer[..length]);
    }
}
