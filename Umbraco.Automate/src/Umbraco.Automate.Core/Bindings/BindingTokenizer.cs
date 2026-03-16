namespace Umbraco.Automate.Core.Bindings;

/// <summary>
/// Parses binding strings like <c>${ trigger.contentName | truncate:100 }</c>
/// into structured <see cref="BindingToken"/> instances.
/// </summary>
internal static class BindingTokenizer
{
    /// <summary>
    /// Tokenizes a single binding (the content between <c>${</c> and <c>}</c>).
    /// </summary>
    /// <param name="binding">The binding content without the ${ } delimiters.</param>
    /// <returns>The parsed token.</returns>
    public static BindingToken Tokenize(string binding)
    {
        var parts = binding.Split('|');
        var path = parts[0].Trim();

        var filters = new List<FilterToken>();
        for (var i = 1; i < parts.Length; i++)
        {
            var filterPart = parts[i].Trim();
            if (string.IsNullOrEmpty(filterPart))
            {
                continue;
            }

            var segments = filterPart.Split(':');
            filters.Add(new FilterToken
            {
                Alias = segments[0].Trim(),
                Args = segments.Length > 1
                    ? segments[1..].Select(s => s.Trim()).ToArray()
                    : [],
            });
        }

        return new BindingToken
        {
            Path = path,
            Filters = filters,
        };
    }

    /// <summary>
    /// Finds all <c>${ ... }</c> bindings in a template string and returns
    /// the raw binding content and its position.
    /// </summary>
    /// <param name="template">The template string.</param>
    /// <returns>Enumerable of (startIndex, length, bindingContent) tuples.</returns>
    public static IEnumerable<(int Start, int Length, string Content)> FindBindings(string template)
    {
        var i = 0;
        while (i < template.Length - 2)
        {
            if (template[i] == '$' && template[i + 1] == '{')
            {
                var start = i;
                var contentStart = i + 2;
                var end = template.IndexOf('}', contentStart);
                if (end == -1)
                {
                    break;
                }

                var content = template[contentStart..end].Trim();
                var length = end - start + 1;
                yield return (start, length, content);
                i = end + 1;
            }
            else
            {
                i++;
            }
        }
    }
}
