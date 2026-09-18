using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Serializes and deserializes editable model objects with automatic encryption of sensitive fields.
/// </summary>
internal sealed class EditableModelSerializer : IEditableModelSerializer
{
    private readonly ISensitiveFieldProtector _protector;
    private readonly IConfigurationReferenceResolver _configReferenceResolver;

    public EditableModelSerializer(ISensitiveFieldProtector protector, IConfigurationReferenceResolver configReferenceResolver)
    {
        _protector = protector;
        _configReferenceResolver = configReferenceResolver;
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
            if (!sensitiveKeys.Contains(property.Key))
            {
                continue;
            }

            switch (property.Value)
            {
                case JsonValue jsonValue:
                    if (Encrypt(jsonValue) is { } encrypted)
                    {
                        jsonObject[property.Key] = encrypted;
                    }

                    break;

                // A sensitive field whose editor stores rows rather than a single scalar — the
                // HTTP Request action's headers, for one. Without this the whole list would be
                // stored in clear, because the field is still marked sensitive but no longer
                // holds a string. Only each row's value is encrypted: key names are not secret,
                // and the key/value editor has to render them to be usable at all.
                case JsonArray jsonArray:
                    EncryptArrayItems(jsonArray);
                    break;
            }
        }
    }

    private void EncryptArrayItems(JsonArray jsonArray)
    {
        for (var i = 0; i < jsonArray.Count; i++)
        {
            switch (jsonArray[i])
            {
                case JsonValue itemValue:
                    if (Encrypt(itemValue) is { } encryptedItem)
                    {
                        jsonArray[i] = encryptedItem;
                    }

                    break;

                case JsonObject itemObject:
                    EncryptRowValue(itemObject);
                    break;
            }
        }
    }

    /// <summary>
    /// Protects the "value" member of a key/value row, whatever case it was written in. The
    /// stored JSON keeps the casing the editor sent, and a case-sensitive lookup that missed
    /// would store the secret in clear without any visible symptom, so the match is made on
    /// the name rather than on the exact spelling.
    /// </summary>
    private void EncryptRowValue(JsonObject row)
    {
        foreach (var member in row.ToList())
        {
            if (!string.Equals(member.Key, "value", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (member.Value is JsonValue rowValue && Encrypt(rowValue) is { } encrypted)
            {
                row[member.Key] = encrypted;
            }
        }
    }

    /// <summary>
    /// Returns the protected form of a JSON string node, or null when there is nothing to
    /// protect — a non-string node, an empty value, or a configuration reference.
    /// </summary>
    private string? Encrypt(JsonValue jsonValue)
    {
        if (!jsonValue.TryGetValue<string>(out var stringValue) || string.IsNullOrEmpty(stringValue))
        {
            return null;
        }

        // Skip encryption for values that contain a configuration reference under an
        // allowed prefix (e.g. "Bearer $Umbraco:Automate:Secrets:ApiKey"). These are
        // pointers to IConfiguration resolved at read time, not actual secrets, and the
        // reference may sit anywhere in the value — not just at the start. The same
        // service drives resolution, so the two decisions cannot drift.
        if (_configReferenceResolver.ContainsReference(stringValue))
        {
            return null;
        }

        return _protector.Protect(stringValue);
    }

    private void DecryptFields(JsonObject jsonObject)
    {
        foreach (var property in jsonObject.ToList())
        {
            switch (property.Value)
            {
                case JsonValue jsonValue:
                    if (Decrypt(jsonValue) is { } decrypted)
                    {
                        jsonObject[property.Key] = decrypted;
                    }

                    break;

                case JsonObject nestedObject:
                    DecryptFields(nestedObject);
                    break;

                // Mirrors EncryptArrayItems so a sensitive list of rows round-trips. Decryption
                // is driven by the value itself rather than by the schema, so every protected
                // string is unwrapped wherever it sits — no assumption about which property
                // inside a row held the secret.
                case JsonArray nestedArray:
                    DecryptArrayItems(nestedArray);
                    break;
            }
        }
    }

    private void DecryptArrayItems(JsonArray jsonArray)
    {
        for (var i = 0; i < jsonArray.Count; i++)
        {
            switch (jsonArray[i])
            {
                case JsonValue itemValue:
                    if (Decrypt(itemValue) is { } decryptedItem)
                    {
                        jsonArray[i] = decryptedItem;
                    }

                    break;

                case JsonObject itemObject:
                    DecryptFields(itemObject);
                    break;

                case JsonArray itemArray:
                    DecryptArrayItems(itemArray);
                    break;
            }
        }
    }

    /// <summary>
    /// Returns the unprotected form of a JSON string node, or null when the node is not a
    /// protected string and should be left alone.
    /// </summary>
    private string? Decrypt(JsonValue jsonValue)
    {
        if (!jsonValue.TryGetValue<string>(out var stringValue) || !_protector.IsProtected(stringValue))
        {
            return null;
        }

        return _protector.Unprotect(stringValue);
    }

}
