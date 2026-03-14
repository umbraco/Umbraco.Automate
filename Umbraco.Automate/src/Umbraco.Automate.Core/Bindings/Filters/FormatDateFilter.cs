using System.Globalization;

namespace Umbraco.Automate.Core.Expressions.Filters;

/// <summary>
/// Formats a DateTime value. Usage: <c>| formatDate:yyyy-MM-dd</c>.
/// </summary>
internal sealed class FormatDateFilter : IExpressionFilter
{
    public string Alias => "formatDate";

    public object? Apply(object? value, string[] args)
    {
        if (args.Length == 0)
        {
            return value?.ToString();
        }

        var format = args[0];

        return value switch
        {
            DateTime dt => dt.ToString(format, CultureInfo.InvariantCulture),
            DateTimeOffset dto => dto.ToString(format, CultureInfo.InvariantCulture),
            _ => value,
        };
    }
}
