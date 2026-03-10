using System.Text.RegularExpressions;

namespace Umbraco.Automate.Core.Expressions.Filters;

/// <summary>
/// Removes HTML tags from a string. Usage: <c>| stripHtml</c>.
/// </summary>
internal sealed partial class StripHtmlFilter : IExpressionFilter
{
    public string Alias => "stripHtml";

    public object? Apply(object? value, string[] args)
        => value is string str ? HtmlTagRegex().Replace(str, string.Empty) : value;

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();
}
