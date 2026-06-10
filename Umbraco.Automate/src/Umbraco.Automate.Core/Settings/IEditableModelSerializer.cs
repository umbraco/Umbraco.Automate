using System.Text.Json;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Serializes and deserializes editable model objects with automatic encryption of sensitive fields.
/// </summary>
public interface IEditableModelSerializer
{
    /// <summary>
    /// Serializes an editable model object to JSON, encrypting sensitive field values based on the schema.
    /// </summary>
    /// <param name="model">The model object to serialize.</param>
    /// <param name="schema">The schema describing which fields are sensitive. If null, no encryption is applied.</param>
    /// <returns>JSON string with sensitive values encrypted, or null if the model is null.</returns>
    string? Serialize(object? model, EditableModelSchema? schema);

    /// <summary>
    /// Deserializes a JSON string, decrypting any encrypted field values.
    /// </summary>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>A <see cref="JsonElement"/> with decrypted values.</returns>
    JsonElement Deserialize(string? json);

    /// <summary>
    /// Deserializes a JSON string to a typed object, decrypting any encrypted field values.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <param name="json">The JSON string to deserialize.</param>
    /// <returns>The deserialized object with decrypted values, or default if the JSON is null or empty.</returns>
    T? Deserialize<T>(string? json);
}
