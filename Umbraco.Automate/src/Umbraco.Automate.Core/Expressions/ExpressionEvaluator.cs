namespace Umbraco.Automate.Core.Expressions;

/// <summary>
/// Resolves <c>${ }</c> expressions against automation run data and applies filters.
/// </summary>
public sealed class ExpressionEvaluator
{
    private readonly IReadOnlyDictionary<string, IExpressionFilter> _filters;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionEvaluator"/> class.
    /// </summary>
    /// <param name="filters">The available expression filters.</param>
    public ExpressionEvaluator(IEnumerable<IExpressionFilter> filters)
    {
        _filters = filters.ToDictionary(f => f.Alias, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Evaluates a template string, replacing all <c>${ ... }</c> expressions with resolved values.
    /// </summary>
    /// <param name="template">The template string containing expressions.</param>
    /// <param name="data">The run data to resolve paths against.</param>
    /// <returns>The evaluated string with all expressions resolved.</returns>
    public string Evaluate(string template, IReadOnlyDictionary<string, object?> data)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var expressions = ExpressionTokenizer.FindExpressions(template).ToList();
        if (expressions.Count == 0)
        {
            return template;
        }

        // Process in reverse to preserve indices
        var result = template;
        for (var i = expressions.Count - 1; i >= 0; i--)
        {
            var (start, length, content) = expressions[i];
            var token = ExpressionTokenizer.Tokenize(content);
            var value = ResolvePath(token.Path, data);

            foreach (var filter in token.Filters)
            {
                if (_filters.TryGetValue(filter.Alias, out var filterImpl))
                {
                    value = filterImpl.Apply(value, filter.Args);
                }
            }

            var replacement = value?.ToString() ?? string.Empty;
            result = string.Concat(result.AsSpan(0, start), replacement, result.AsSpan(start + length));
        }

        return result;
    }

    /// <summary>
    /// Resolves a dot-separated path against the run data dictionary.
    /// Supports nested dictionaries (e.g. "trigger.contentName" → data["trigger"]["contentName"]).
    /// </summary>
    internal static object? ResolvePath(string path, IReadOnlyDictionary<string, object?> data)
    {
        var segments = path.Split('.');
        object? current = data;

        foreach (var segment in segments)
        {
            if (current is IReadOnlyDictionary<string, object?> dict)
            {
                if (!dict.TryGetValue(segment, out current))
                {
                    return null;
                }
            }
            else if (current is IDictionary<string, object?> mutableDict)
            {
                if (!mutableDict.TryGetValue(segment, out current))
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        return current;
    }
}
