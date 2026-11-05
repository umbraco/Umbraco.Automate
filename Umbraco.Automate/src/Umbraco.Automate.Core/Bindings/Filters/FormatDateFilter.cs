using System.Globalization;

namespace Umbraco.Automate.Core.Bindings.Filters;

/// <summary>
/// Formats a DateTime value. Usage: <c>| formatDate:yyyy-MM-dd</c>. With no format, dates
/// render as ISO 8601 round-trip, the same as a bare binding, on every server.
/// </summary>
internal sealed class FormatDateFilter : IBindingFilter
{
    public string Alias => "formatDate";

    public object? Apply(object? value, string[] args)
    {
        var format = args.Length == 0 ? "O" : args[0];

        return value switch
        {
            DateTime dt => dt.ToString(format, CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString(format, CultureInfo.InvariantCulture),
            DateOnly d => d.ToString(format, CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString(format, CultureInfo.InvariantCulture),

            // Data read with System.Text.Json (trigger output at the trigger edge, webhook
            // bodies, HTTP responses) carries dates as ISO 8601 strings.
            string s when TryParseIsoDate(s, out var date) => Apply(date, args),
            _ => value,
        };
    }

    /// <summary>
    /// Reads an ISO 8601 date string as the typed value it was written from: a <see cref="DateOnly"/>
    /// for a bare date, a <see cref="DateTimeOffset"/> when it carries an offset (so the offset is
    /// kept), otherwise a <see cref="DateTime"/> (UTC for a trailing Z, unspecified with no zone).
    /// Any other string is left alone rather than guessed at in some culture.
    /// </summary>
    private static bool TryParseIsoDate(string value, out object date)
    {
        date = value;
        if (!StartsWithIsoDate(value))
        {
            return false;
        }

        if (value.Length == 10)
        {
            var parsedDate = DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d);
            date = d;
            return parsedDate;
        }

        if (value[10] != 'T')
        {
            return false;
        }

        // A sign after the date part can only be a UTC offset.
        if (value.IndexOfAny(['+', '-'], 11) >= 0)
        {
            var parsedOffset = DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto);
            date = dto;
            return parsedOffset;
        }

        var parsed = DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt);
        date = dt;
        return parsed;
    }

    private static bool StartsWithIsoDate(string value)
        => value.Length >= 10
           && char.IsAsciiDigit(value[0]) && char.IsAsciiDigit(value[1]) && char.IsAsciiDigit(value[2]) && char.IsAsciiDigit(value[3])
           && value[4] == '-'
           && char.IsAsciiDigit(value[5]) && char.IsAsciiDigit(value[6])
           && value[7] == '-'
           && char.IsAsciiDigit(value[8]) && char.IsAsciiDigit(value[9]);
}
