using System.Globalization;
using System.Text.Json;
using Umbraco.Automate.Web.Api.Management.Common.Json;

namespace Umbraco.Automate.Tests.Unit.Json;

// The Management API runs with the backoffice user's culture. Danish (and Finnish) use '.' as
// the time separator, so a culture-sensitive date format wrote "10.00.00", which browsers can't
// parse. Dates on the wire must be ISO 8601 whatever the request culture is.
public class UtcDateTimeJsonConverterTests
{
    private static readonly DateTime Utc = new(2026, 11, 4, 10, 0, 0, 123, DateTimeKind.Utc);
    private const string IsoJson = "\"2026-11-04T10:00:00.123Z\"";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Serialize_OnDotTimeSeparatorCulture_WritesIso8601(bool nullable)
    {
        var json = InCulture("da-DK", () => nullable
            ? JsonSerializer.Serialize<DateTime?>(Utc, Options)
            : JsonSerializer.Serialize(Utc, Options));

        json.ShouldBe(IsoJson);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Deserialize_OnDotTimeSeparatorCulture_ReadsIso8601(bool nullable)
    {
        var value = InCulture("da-DK", () => nullable
            ? JsonSerializer.Deserialize<DateTime?>(IsoJson, Options)
            : JsonSerializer.Deserialize<DateTime>(IsoJson, Options));

        value.ShouldBe(Utc);
    }

    private static JsonSerializerOptions Options { get; } = new()
    {
        Converters = { new UtcDateTimeJsonConverter(), new UtcNullableDateTimeJsonConverter() },
    };

    private static T InCulture<T>(string culture, Func<T> action)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try
        {
            return action();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
