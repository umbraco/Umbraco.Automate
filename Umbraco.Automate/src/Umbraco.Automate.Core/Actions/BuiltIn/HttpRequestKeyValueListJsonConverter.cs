using System.Text.Json;
using System.Text.Json.Serialization;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Reads a <see cref="HttpRequestKeyValue"/> list from either the current array-of-rows shape
/// or the shape saved before the key/value editor existed, where headers were a single JSON
/// object string such as <c>{"Authorization":"Bearer x"}</c>.
/// </summary>
/// <remarks>
/// A read-time migration in the same spirit as <see cref="Settings.SingleValueArrayConverterFactory"/>:
/// steps saved by an older version are not rewritten in the database, they are simply understood
/// on the way in, and are stored in the new shape the next time the step is saved.
/// <para>
/// Genuinely malformed legacy data throws instead of being swallowed. The old
/// <c>HttpRequestAction.ApplyHeaders</c> caught <see cref="JsonException"/> and sent the request
/// with no headers at all, so a typo silently produced an unauthenticated call; now the step
/// fails with a message naming the problem, and the failure is terminal so it is not retried.
/// </para>
/// </remarks>
internal sealed class HttpRequestKeyValueListJsonConverter : JsonConverter<List<HttpRequestKeyValue>>
{
    /// <inheritdoc />
    public override List<HttpRequestKeyValue>? Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.StartArray:
                return ReadRows(ref reader, options);

            // The legacy shape: the whole header map as a JSON object string.
            case JsonTokenType.String:
                return ReadLegacyString(reader.GetString());

            // Defensive: a caller that sends the map as a real JSON object rather than a
            // string is expressing the same intent, so accept it on the same terms.
            case JsonTokenType.StartObject:
                return FromMap(JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(ref reader, options));

            default:
                throw new JsonException(
                    $"Cannot read headers from a JSON {reader.TokenType} token. Expected a list of "
                    + "key/value rows, or the legacy JSON object string.");
        }
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, List<HttpRequestKeyValue> value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var row in value)
        {
            JsonSerializer.Serialize(writer, row, options);
        }

        writer.WriteEndArray();
    }

    private static List<HttpRequestKeyValue> ReadRows(ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var rows = new List<HttpRequestKeyValue>();

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var row = JsonSerializer.Deserialize<HttpRequestKeyValue>(ref reader, options);
            if (row is not null)
            {
                rows.Add(row);
            }
        }

        return rows;
    }

    private static List<HttpRequestKeyValue> ReadLegacyString(string? legacy)
    {
        if (string.IsNullOrWhiteSpace(legacy))
        {
            return [];
        }

        Dictionary<string, JsonElement>? map;
        try
        {
            map = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(legacy);
        }
        catch (JsonException ex)
        {
            throw new JsonException(
                "The headers saved for this step are not valid JSON and could not be read: "
                + $"{ex.Message} Open the step and re-enter the headers as key/value rows.",
                ex);
        }

        return FromMap(map);
    }

    private static List<HttpRequestKeyValue> FromMap(Dictionary<string, JsonElement>? map)
    {
        if (map is null)
        {
            return [];
        }

        var rows = new List<HttpRequestKeyValue>(map.Count);
        foreach (var (key, element) in map)
        {
            rows.Add(new HttpRequestKeyValue
            {
                Key = key,

                // Header values were always strings, but a number or boolean written into the
                // legacy JSON is unambiguous, so carry it across rather than failing the step.
                Value = element.ValueKind switch
                {
                    JsonValueKind.String => element.GetString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    JsonValueKind.Object or JsonValueKind.Array => throw new JsonException(
                        $"The saved header '{key}' has a {element.ValueKind} value. Header values "
                        + "must be text. Open the step and re-enter the headers as key/value rows."),
                    _ => element.GetRawText(),
                },
            });
        }

        return rows;
    }
}
