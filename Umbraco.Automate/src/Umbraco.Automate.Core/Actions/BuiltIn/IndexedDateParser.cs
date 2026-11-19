using System.Globalization;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Reads a date back from the string an Examine index search result holds for it.
/// Shared by <see cref="FindContentAction"/> and <see cref="FindMediaAction"/>.
/// </summary>
internal static class IndexedDateParser
{
    /// <summary>
    /// Parses an indexed date, returning <see langword="default"/> when the value is missing or unrecognised.
    /// </summary>
    internal static DateTime Parse(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return default;
        }

        // Lucene's DateTools shape, yyyyMMddHHmmssfff (milliseconds resolution). Checked before
        // ticks because these 17 digits would otherwise parse as a (wrong) tick count.
        if (DateTime.TryParseExact(raw, "yyyyMMddHHmmssfff", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var exact))
        {
            return exact;
        }

        // Examine's DateTime field type (which Umbraco uses for createDate and updateDate)
        // stores the value as ticks. Umbraco keeps these dates in UTC.
        if (long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            && ticks <= DateTime.MaxValue.Ticks)
        {
            return new DateTime(ticks, DateTimeKind.Utc);
        }

        // Fallback for indexers or custom fields that emit ISO-8601.
        return DateTime.TryParse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value)
            ? value
            : default;
    }
}
