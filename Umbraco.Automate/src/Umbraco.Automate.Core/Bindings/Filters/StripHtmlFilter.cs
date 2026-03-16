using System.Text.RegularExpressions;

namespace Umbraco.Automate.Core.Bindings.Filters;

/// <summary>
/// Removes HTML tags from a string. Usage: <c>| stripHtml</c>.
/// </summary>
internal sealed partial class StripHtmlFilter : IBindingFilter
{
    public string Alias => "stripHtml";

    public object? Apply(object? value, string[] args)
        => value is string str ? HtmlTagRegex().Replace(str, string.Empty) : value;

    [GeneratedRegex("<[^>]+>", RegexOptions.Compiled)]
    private static partial Regex HtmlTagRegex();
}
