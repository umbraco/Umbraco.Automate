namespace Umbraco.Automate.Core.Expressions;

/// <summary>
/// Parses expression strings like <c>${ trigger.contentName | truncate:100 }</c>
/// into structured <see cref="ExpressionToken"/> instances.
/// </summary>
internal static class ExpressionTokenizer
{
    /// <summary>
    /// Tokenizes a single expression (the content between <c>${</c> and <c>}</c>).
    /// </summary>
    /// <param name="expression">The expression content without the ${ } delimiters.</param>
    /// <returns>The parsed token.</returns>
    public static ExpressionToken Tokenize(string expression)
    {
        var parts = expression.Split('|');
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

        return new ExpressionToken
        {
            Path = path,
            Filters = filters,
        };
    }

    /// <summary>
    /// Finds all <c>${ ... }</c> expressions in a template string and returns
    /// the raw expression content and its position.
    /// </summary>
    /// <param name="template">The template string.</param>
    /// <returns>Enumerable of (startIndex, length, expressionContent) tuples.</returns>
    public static IEnumerable<(int Start, int Length, string Content)> FindExpressions(string template)
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
