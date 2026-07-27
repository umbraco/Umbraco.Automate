using Jint.Runtime;

namespace Umbraco.Automate.Core.Scripting;

/// <summary>
/// A minimal implementation of the web <c>Headers</c> interface exposed to scripts, backing the
/// <c>fetch</c> request/response header surface.
/// </summary>
public sealed class Headers
{
    private readonly Dictionary<string, List<string?>> _headers;
    private readonly List<string> _setCookie;
    private readonly bool _immutable;

    internal Headers(HttpResponseMessage response)
    {
        _headers = response.Headers.Concat(response.Content.Headers)
            .Where(x => !x.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value.OfType<string?>().ToList(), StringComparer.OrdinalIgnoreCase);

        _setCookie = response.Headers
            .Where(x => x.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
            .SelectMany(x => x.Value)
            .ToList();

        _immutable = true;
    }

    /// <summary>Initializes a new, empty, mutable <see cref="Headers"/> instance.</summary>
    public Headers()
    {
        _headers = new(StringComparer.OrdinalIgnoreCase);
        _setCookie = new();
    }

    /// <summary>Initializes a new <see cref="Headers"/> instance copied from another.</summary>
    public Headers(Headers headers)
    {
        // Copy the value lists too: sharing them would let a mutable copy append to the headers
        // of the (immutable) response it was copied from.
        _headers = headers._headers.ToDictionary(x => x.Key, x => new List<string?>(x.Value), StringComparer.OrdinalIgnoreCase);
        _setCookie = new(headers._setCookie);
    }

    /// <summary>Initializes a new <see cref="Headers"/> instance from key/value pairs.</summary>
    public Headers(IEnumerable<KeyValuePair<string, string?>> headers)
    {
        _headers = headers.GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Select(x => x.Value).ToList(), StringComparer.OrdinalIgnoreCase);
        _setCookie = new();
    }

    /// <summary>Initializes a new <see cref="Headers"/> instance from key/multi-value pairs.</summary>
    public Headers(IEnumerable<KeyValuePair<string, IReadOnlyCollection<string?>>> headers)
    {
        _headers = headers.ToDictionary(x => x.Key, x => x.Value.ToList(), StringComparer.OrdinalIgnoreCase);
        _setCookie = new();
    }

    /// <summary>Initializes a new <see cref="Headers"/> instance from key/value tuples.</summary>
    public Headers(IEnumerable<(string, string?)> headers)
    {
        _headers = headers.GroupBy(x => x.Item1).ToDictionary(x => x.Key, x => x.Select(x => x.Item2).ToList(), StringComparer.OrdinalIgnoreCase);
        _setCookie = new();
    }

    internal Dictionary<string, List<string?>> AllHeaders => _headers;

    /// <summary>Appends a value to the header with the given key.</summary>
    public void Append(string key, string value)
    {
        if (_immutable) throw new JavaScriptException("Failed to execute 'append' on 'Headers': Headers are immutable");

        if (key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
        {
            _setCookie.Add(value);
        }
        else if (_headers.TryGetValue(key, out var existing))
        {
            existing.Add(value);
        }
        else
        {
            _headers.Add(key, new() { value });
        }
    }

    /// <summary>Deletes the header with the given key.</summary>
    public void Delete(string key)
    {
        if (_immutable) throw new JavaScriptException("Failed to execute 'delete' on 'Headers': Headers are immutable");

        if (key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
        {
            _setCookie.Clear();
        }
        else
        {
            _headers.Remove(key);
        }
    }

    /// <summary>Returns the header entries as key/value arrays.</summary>
    public IEnumerable<string[]> Entries()
    {
        foreach (var (header, values) in _headers)
        {
            yield return [header, string.Join(", ", values)];
        }
    }

    /// <summary>Invokes a callback for each header.</summary>
    public void ForEach(Action<(string value, string key)> callbackFn)
    {
        foreach (var (header, values) in _headers)
        {
            callbackFn((string.Join(", ", values), header));
        }
    }

    /// <summary>Gets the combined value of the header with the given key, or <c>null</c>.</summary>
    public string? Get(string key) => _headers.TryGetValue(key, out var values) ? string.Join(", ", values) : null;

    /// <summary>Gets all <c>Set-Cookie</c> values.</summary>
    public IEnumerable<string> GetSetCookie() => _setCookie;

    /// <summary>Returns whether a header with the given key exists.</summary>
    public bool Has(string key) => _headers.ContainsKey(key);

    /// <summary>Returns the header keys.</summary>
    public IEnumerable<string> Keys() => _headers.Keys;

    /// <summary>Sets the header with the given key, replacing any existing values.</summary>
    public void Set(string key, string value)
    {
        if (_immutable) throw new JavaScriptException("Failed to execute 'set' on 'Headers': Headers are immutable");

        if (key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
        {
            _setCookie.Clear();
            _setCookie.Add(value);
        }
        else
        {
            _headers[key] = new List<string?> { value };
        }
    }

    /// <summary>Returns the combined header values.</summary>
    public IEnumerable<string> Values() => _headers.Values.Select(x => string.Join(", ", x));
}
