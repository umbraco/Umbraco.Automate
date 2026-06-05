using System.Collections;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Lets scalar settings properties accept a single-element JSON array in addition to a
/// raw scalar. Umbraco's Dropdown property editor (and a few others) always persists its
/// value as <c>["X"]</c> even when configured with <c>multiple: false</c>, but our settings
/// POCOs declare those properties as <c>string</c>, <c>int</c>, etc. Without this
/// converter, deserialization throws on the array → scalar mismatch.
/// </summary>
/// <remarks>
/// Empty arrays resolve to the default value of the target type (so an unselected dropdown
/// becomes <c>null</c> for nullable references and <c>0</c> / <c>false</c> for value
/// types). Multi-element arrays still throw — silently dropping extra items would mask
/// genuine schema mismatches.
/// </remarks>
internal sealed class SingleValueArrayConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert)
    {
        // Skip anything collection-shaped — those have their own converters and should not
        // be auto-flattened from outer arrays.
        if (typeToConvert != typeof(string)
            && typeof(IEnumerable).IsAssignableFrom(typeToConvert))
        {
            return false;
        }

        var underlying = Nullable.GetUnderlyingType(typeToConvert) ?? typeToConvert;

        return underlying == typeof(string)
            || underlying.IsPrimitive
            || underlying.IsEnum
            || underlying == typeof(decimal)
            || underlying == typeof(Guid)
            || underlying == typeof(DateTime)
            || underlying == typeof(DateTimeOffset)
            || underlying == typeof(TimeSpan);
    }

    /// <inheritdoc />
    public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter?)Activator.CreateInstance(
            typeof(SingleValueArrayConverter<>).MakeGenericType(typeToConvert));

    /// <summary>
    /// Inner deserialization (and all writes) re-enters <see cref="JsonSerializer"/> with a
    /// cached options instance that omits this factory, so the recursive call hits STJ's
    /// built-in scalar handling instead of looping back into us.
    /// </summary>
    private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> InnerOptionsCache = new();

    private static JsonSerializerOptions GetInnerOptions(JsonSerializerOptions options)
        => InnerOptionsCache.GetValue(options, original =>
        {
            var copy = new JsonSerializerOptions(original);
            for (var i = copy.Converters.Count - 1; i >= 0; i--)
            {
                if (copy.Converters[i] is SingleValueArrayConverterFactory)
                {
                    copy.Converters.RemoveAt(i);
                }
            }

            return copy;
        });

    private sealed class SingleValueArrayConverter<T> : JsonConverter<T>
    {
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray)
            {
                return JsonSerializer.Deserialize<T>(ref reader, GetInnerOptions(options));
            }

            // Empty array → default. Models an unselected dropdown.
            if (!reader.Read() || reader.TokenType == JsonTokenType.EndArray)
            {
                return default;
            }

            var value = JsonSerializer.Deserialize<T>(ref reader, GetInnerOptions(options));

            if (!reader.Read() || reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException(
                    $"Cannot deserialize multi-element JSON array into '{typeof(T).Name}'. "
                    + "Single-value dropdowns must contain at most one item.");
            }

            return value;
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, GetInnerOptions(options));
    }
}
