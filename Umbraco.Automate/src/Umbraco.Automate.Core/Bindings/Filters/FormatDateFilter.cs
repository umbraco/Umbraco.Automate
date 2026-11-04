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
            _ => value,
        };
    }
}
