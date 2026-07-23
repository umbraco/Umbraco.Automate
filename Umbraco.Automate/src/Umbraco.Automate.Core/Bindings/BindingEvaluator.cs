using System.Text.Json;
using Newtonsoft.Json.Linq;

namespace Umbraco.Automate.Core.Bindings;

/// <summary>
/// Resolves <c>${ }</c> bindings against automation run data and applies filters.
/// </summary>
public sealed class BindingEvaluator
{
    private readonly IReadOnlyDictionary<string, IBindingFilter> _filters;

    /// <summary>
    /// Initializes a new instance of the <see cref="BindingEvaluator"/> class.
    /// </summary>
    /// <param name="filters">The available binding filters.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when two filters declare the same (case-insensitive) alias. Filters are
    /// auto-discovered across all loaded assemblies, so a fail-fast with both type names
    /// is clearer than the opaque duplicate-key error from <see cref="Enumerable.ToDictionary{TSource,TKey}(IEnumerable{TSource},Func{TSource,TKey},IEqualityComparer{TKey}?)"/>,
    /// and avoids silently shadowing one filter with another in non-deterministic discovery order.
    /// </exception>
    public BindingEvaluator(BindingFilterCollection filters)
    {
        var byAlias = new Dictionary<string, IBindingFilter>(StringComparer.OrdinalIgnoreCase);
        foreach (var filter in filters)
        {
            if (!byAlias.TryAdd(filter.Alias, filter))
            {
                throw new InvalidOperationException(
                    $"Duplicate binding filter alias '{filter.Alias}' declared by " +
                    $"'{byAlias[filter.Alias].GetType().FullName}' and '{filter.GetType().FullName}'. " +
                    "Each binding filter must have a unique alias.");
            }
        }

        _filters = byAlias;
    }

    /// <summary>
    /// Evaluates a template string, replacing all <c>${ ... }</c> bindings with resolved values.
    /// </summary>
    /// <param name="template">The template string containing bindings.</param>
    /// <param name="data">The run data to resolve paths against.</param>
    /// <returns>The evaluated string with all bindings resolved.</returns>
    public string Evaluate(string template, IReadOnlyDictionary<string, object?> data)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var bindings = BindingTokenizer.FindBindings(template).ToList();
        if (bindings.Count == 0)
        {
            return template;
        }

        // Process in reverse to preserve indices
        var result = template;
        for (var i = bindings.Count - 1; i >= 0; i--)
        {
            var (start, length, content) = bindings[i];
            var token = BindingTokenizer.Tokenize(content);
            var value = ResolvePath(token.Path, data);

            foreach (var filter in token.Filters)
            {
                if (_filters.TryGetValue(filter.Alias, out var filterImpl))
                {
                    value = filterImpl.Apply(value, filter.Args);
                }
            }

            var replacement = Stringify(value);
            result = string.Concat(result.AsSpan(0, start), replacement, result.AsSpan(start + length));
        }

        return result;
    }

    /// <summary>
    /// Evaluates a template that is expected to be a single unfiltered binding
    /// (e.g. <c>${ steps.x.matches }</c>) and returns the raw resolved value —
    /// useful for callers that need to inspect collection emptiness or types
    /// rather than a stringified form. Templates with surrounding text or filters
    /// fall back to the string-producing <see cref="Evaluate"/>.
    /// </summary>
    public object? EvaluateRaw(string template, IReadOnlyDictionary<string, object?> data)
    {
        if (string.IsNullOrEmpty(template))
        {
            return template;
        }

        var bindings = BindingTokenizer.FindBindings(template).ToList();
        if (bindings.Count == 1)
        {
            var (start, length, content) = bindings[0];
            if (start == 0 && length == template.Length)
            {
                var token = BindingTokenizer.Tokenize(content);
                if (token.Filters.Count == 0)
                {
                    return ResolvePath(token.Path, data);
                }
            }
        }

        return Evaluate(template, data);
    }

    /// <summary>
    /// Resolves a dot-separated path against the run data dictionary.
    /// Supports nested dictionaries, array index access (<c>items[0]</c>),
    /// dictionary key access (<c>headers["Content-Type"]</c>),
    /// bare numeric segments, and <c>length</c>/<c>count</c> pseudo-properties on lists.
    /// </summary>
    internal static object? ResolvePath(string path, IReadOnlyDictionary<string, object?> data)
    {
        var segments = path.Split('.');
        object? current = data;

        foreach (var segment in segments)
        {
            if (current is null)
            {
                return null;
            }

            if (TryParseBracketAccessor(segment, out var name, out var accessor))
            {
                // Compound segment: name[accessor] — resolve name first, then apply accessor.
                if (!string.IsNullOrEmpty(name))
                {
                    current = EnsureUnwrapped(current);
                    current = ResolveKey(current, name);
                    if (current is null)
                    {
                        return null;
                    }
                }

                current = EnsureUnwrapped(current);

                if (accessor.IsNumeric)
                {
                    current = ResolveIndex(current, accessor.Index);
                }
                else
                {
                    current = ResolveKey(current, accessor.Key!);
                }
            }
            else if (IsLengthSegment(segment))
            {
                current = EnsureUnwrapped(current);
                current = ResolveLength(current);
            }
            else if (int.TryParse(segment, out var bareIndex))
            {
                // Bare numeric segment: try array index first, fall back to dict key.
                current = EnsureUnwrapped(current);
                var indexed = ResolveIndex(current, bareIndex);
                current = indexed ?? ResolveKey(current, segment);
            }
            else
            {
                current = EnsureUnwrapped(current);
                current = ResolveKey(current, segment);
            }
        }

        // Final unwrap in case the last resolved value is a JToken (e.g. JValue wrapping
        // a GUID string) — ensures downstream Stringify sees a plain CLR value.
        return EnsureUnwrapped(current);
    }

    /// <summary>
    /// Converts a resolved value to its string representation.
    /// Lists and dictionaries are serialized to JSON; primitives use <see cref="object.ToString"/>.
    /// </summary>
    internal static string Stringify(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string s)
        {
            return s;
        }

        if (value is IList<object?> or IDictionary<string, object?> or IReadOnlyDictionary<string, object?>)
        {
            return JsonSerializer.Serialize(value, Dispatch.JsonOptions.Default);
        }

        return value.ToString() ?? string.Empty;
    }

    /// <summary>
    /// Parses a bracket accessor from a segment. Supports:
    /// <list type="bullet">
    ///   <item><c>name[0]</c> — numeric index</item>
    ///   <item><c>name["key"]</c> or <c>name['key']</c> — quoted string key</item>
    ///   <item><c>[0]</c> — bare numeric index (empty name)</item>
    ///   <item><c>["key"]</c> — bare quoted key (empty name)</item>
    /// </list>
    /// </summary>
    private static bool TryParseBracketAccessor(string segment, out string name, out BracketAccessor accessor)
    {
        var bracketStart = segment.IndexOf('[');
        if (bracketStart >= 0 && segment.EndsWith(']'))
        {
            name = segment[..bracketStart];
            var inner = segment[(bracketStart + 1)..^1];

            // Numeric index: [0], [42]
            if (int.TryParse(inner, out var index))
            {
                accessor = BracketAccessor.Numeric(index);
                return true;
            }

            // Quoted string key: ["key"] or ['key']
            if (inner.Length >= 2 &&
                ((inner[0] == '"' && inner[^1] == '"') ||
                 (inner[0] == '\'' && inner[^1] == '\'')))
            {
                accessor = BracketAccessor.StringKey(inner[1..^1]);
                return true;
            }
        }

        name = string.Empty;
        accessor = default;
        return false;
    }

    private static object? ResolveKey(object? current, string key)
    {
        // Lookups must not depend on the dictionary's own comparer: a WorkflowCore
        // persistence round-trip through Newtonsoft rebuilds nested dictionaries with
        // the default (case-sensitive) comparer, so a PascalCase binding would miss a
        // camelCase-stored key. Try the O(1) exact match first, then fall back to the
        // first case-insensitive match. Step output originates from a single POCO, so
        // keys differing only by case are not expected; if they ever occurred the exact
        // match still wins and the fallback's tie-break is simply enumeration order.
        if (current is IReadOnlyDictionary<string, object?> roDict)
        {
            if (roDict.TryGetValue(key, out var val))
            {
                return val;
            }

            return roDict.FirstOrDefault(kvp => string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        }

        if (current is IDictionary<string, object?> mDict)
        {
            if (mDict.TryGetValue(key, out var val))
            {
                return val;
            }

            return mDict.FirstOrDefault(kvp => string.Equals(kvp.Key, key, StringComparison.OrdinalIgnoreCase)).Value;
        }

        return null;
    }

    private static object? ResolveIndex(object? current, int index)
    {
        if (current is IList<object?> list)
        {
            return index >= 0 && index < list.Count ? list[index] : null;
        }

        if (current is IReadOnlyList<object?> roList)
        {
            return index >= 0 && index < roList.Count ? roList[index] : null;
        }

        return null;
    }

    private static bool IsLengthSegment(string segment)
        => string.Equals(segment, "length", StringComparison.OrdinalIgnoreCase)
           || string.Equals(segment, "count", StringComparison.OrdinalIgnoreCase);

    private static object? ResolveLength(object? current)
    {
        if (current is ICollection<object?> col)
        {
            return col.Count;
        }

        if (current is IReadOnlyCollection<object?> roCol)
        {
            return roCol.Count;
        }

        return null;
    }

    /// <summary>
    /// Normalises a value to plain .NET types before traversal. Handles two round-trip
    /// artefacts from the WorkflowCore persistence layer:
    /// <list type="bullet">
    /// <item>JSON-string values (when nested data was flattened to text).</item>
    /// <item><see cref="JToken"/> instances (when Newtonsoft deserialises
    /// <c>Dictionary&lt;string, object?&gt;</c> values without <see cref="Newtonsoft.Json.TypeNameHandling"/>
    /// metadata — nested objects become <see cref="JObject"/> and arrays become <see cref="JArray"/>,
    /// neither of which satisfies the strict <c>IDictionary&lt;string,object?&gt;</c>/<c>IList&lt;object?&gt;</c>
    /// checks <see cref="ResolveKey"/> and <see cref="ResolveIndex"/> rely on).</item>
    /// </list>
    /// </summary>
    private static object? EnsureUnwrapped(object? value)
    {
        // JToken → plain types first so downstream traversal sees Dictionary/List.
        if (value is JToken jtoken)
        {
            return UnwrapJToken(jtoken);
        }

        if (value is not string jsonString || string.IsNullOrWhiteSpace(jsonString))
        {
            return value;
        }

        var trimmed = jsonString.AsSpan().Trim();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return value;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonString);
            return Dispatch.JsonOptions.UnwrapJsonElement(doc.RootElement);
        }
        catch (JsonException)
        {
            return value;
        }
    }

    private static object? UnwrapJToken(JToken token)
    {
        switch (token.Type)
        {
            case JTokenType.Object:
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in ((JObject)token).Properties())
                {
                    dict[prop.Name] = UnwrapJToken(prop.Value);
                }
                return dict;

            case JTokenType.Array:
                var list = new List<object?>();
                foreach (var item in (JArray)token)
                {
                    list.Add(UnwrapJToken(item));
                }
                return list;

            case JTokenType.Null:
            case JTokenType.Undefined:
                return null;

            default:
                // JValue (string/int/bool/float/date/etc) — extract the underlying CLR value.
                return ((JValue)token).Value;
        }
    }

    private readonly record struct BracketAccessor(bool IsNumeric, int Index, string? Key)
    {
        public static BracketAccessor Numeric(int index) => new(true, index, null);

        public static BracketAccessor StringKey(string key) => new(false, 0, key);
    }
}
