using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Serializes and deserializes editable model objects with automatic encryption of sensitive fields.
/// </summary>
internal sealed class EditableModelSerializer : IEditableModelSerializer
{
    private readonly ISensitiveFieldProtector _protector;
    private readonly IReadOnlyList<string> _allowedConfigKeyPrefixes;

    public EditableModelSerializer(ISensitiveFieldProtector protector, IOptions<AutomateOptions>? options = null)
    {
        _protector = protector;

        // Mirror EditableModelResolver: fall back to the secure defaults when constructed without
        // options so the "is this a configuration reference?" decision uses the same allow-list
        // the resolver uses to substitute. Production always supplies options via DI.
        var automateOptions = options?.Value ?? new AutomateOptions();
        _allowedConfigKeyPrefixes = automateOptions.AllowedConfigurationKeyPrefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
    }

    /// <inheritdoc />
    public string? Serialize(object? model, EditableModelSchema? schema)
    {
        if (model is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(model, JsonOptions.Settings);

        if (schema is null || !schema.Fields.Any(f => f.IsSensitive))
        {
            return json;
        }

        var jsonNode = JsonNode.Parse(json);
        if (jsonNode is JsonObject jsonObject)
        {
            EncryptSensitiveFields(jsonObject, schema);
            return jsonObject.ToJsonString(JsonOptions.Settings);
        }

        return json;
    }

    /// <inheritdoc />
    public JsonElement Deserialize(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        var jsonNode = JsonNode.Parse(json);
        if (jsonNode is JsonObject jsonObject)
        {
            DecryptFields(jsonObject);
            var decryptedJson = jsonObject.ToJsonString(JsonOptions.Settings);
            return JsonSerializer.Deserialize<JsonElement>(decryptedJson, JsonOptions.Settings);
        }

        return JsonSerializer.Deserialize<JsonElement>(json, JsonOptions.Settings);
    }

    /// <inheritdoc />
    public T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        var jsonNode = JsonNode.Parse(json);
        if (jsonNode is JsonObject jsonObject)
        {
            DecryptFields(jsonObject);
            var decryptedJson = jsonObject.ToJsonString(JsonOptions.Settings);
            return JsonSerializer.Deserialize<T>(decryptedJson, JsonOptions.Settings);
        }

        return JsonSerializer.Deserialize<T>(json, JsonOptions.Settings);
    }

    private void EncryptSensitiveFields(JsonObject jsonObject, EditableModelSchema schema)
    {
        var sensitiveKeys = schema.Fields
            .Where(f => f.IsSensitive)
            .Select(f => f.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var property in jsonObject.ToList())
        {
            if (sensitiveKeys.Contains(property.Key) && property.Value is JsonValue jsonValue)
            {
                var stringValue = jsonValue.GetValue<string>();
                if (!string.IsNullOrEmpty(stringValue))
                {
                    // Skip encryption for values that contain a configuration reference under an
                    // allowed prefix (e.g. "Bearer $Umbraco:Automate:Secrets:ApiKey"). These are
                    // pointers to IConfiguration resolved at read time, not actual secrets, and the
                    // reference may sit anywhere in the value — not just at the start. The same
                    // scanner drives resolution, so the two decisions cannot drift.
                    if (ConfigurationReferenceScanner.ContainsReference(stringValue, _allowedConfigKeyPrefixes))
                    {
                        continue;
                    }

                    var encrypted = _protector.Protect(stringValue);
                    jsonObject[property.Key] = encrypted;
                }
            }
        }
    }

    private void DecryptFields(JsonObject jsonObject)
    {
        foreach (var property in jsonObject.ToList())
        {
            if (property.Value is JsonValue jsonValue)
            {
                try
                {
                    var stringValue = jsonValue.GetValue<string>();
                    if (_protector.IsProtected(stringValue))
                    {
                        var decrypted = _protector.Unprotect(stringValue);
                        jsonObject[property.Key] = decrypted;
                    }
                }
                catch (InvalidOperationException)
                {
                    // Not a string value, skip.
                }
            }
            else if (property.Value is JsonObject nestedObject)
            {
                DecryptFields(nestedObject);
            }
        }
    }

}
