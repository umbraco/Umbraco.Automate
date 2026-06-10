using System.Text.Json;

namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Shared comparison helpers for versionable entity adapters.
/// </summary>
internal static class VersionComparer
{
    /// <summary>
    /// Adds a <see cref="ValueChange"/> if the two scalar values differ.
    /// </summary>
    public static void CompareScalar(List<ValueChange> changes, string path, string? oldValue, string? newValue)
    {
        if (oldValue != newValue)
        {
            changes.Add(new ValueChange(path, oldValue, newValue));
        }
    }

    /// <summary>
    /// Compares two <c>Dictionary&lt;string, object?&gt;</c> instances key-by-key.
    /// </summary>
    public static void CompareObjectDictionary(
        List<ValueChange> changes,
        string prefix,
        IDictionary<string, object?>? from,
        IDictionary<string, object?>? to)
    {
        from ??= new Dictionary<string, object?>();
        to ??= new Dictionary<string, object?>();

        var allKeys = from.Keys.Union(to.Keys);

        foreach (var key in allKeys)
        {
            from.TryGetValue(key, out var oldVal);
            to.TryGetValue(key, out var newVal);
            CompareScalar(changes, $"{prefix}.{key}", Stringify(oldVal), Stringify(newVal));
        }
    }

    /// <summary>
    /// Compares two <c>Dictionary&lt;string, string&gt;</c> instances key-by-key.
    /// </summary>
    public static void CompareStringDictionary(
        List<ValueChange> changes,
        string prefix,
        IDictionary<string, string>? from,
        IDictionary<string, string>? to)
    {
        from ??= new Dictionary<string, string>();
        to ??= new Dictionary<string, string>();

        var allKeys = from.Keys.Union(to.Keys);

        foreach (var key in allKeys)
        {
            from.TryGetValue(key, out var oldVal);
            to.TryGetValue(key, out var newVal);
            CompareScalar(changes, $"{prefix}.{key}", oldVal, newVal);
        }
    }

    /// <summary>
    /// Diffs two sets of GUIDs, emitting added/removed changes.
    /// </summary>
    public static void CompareGuidSets(List<ValueChange> changes, string path, IEnumerable<Guid> from, IEnumerable<Guid> to)
    {
        var fromSet = from.ToHashSet();
        var toSet = to.ToHashSet();

        foreach (var added in toSet.Except(fromSet))
        {
            changes.Add(new ValueChange(path, null, added.ToString()));
        }

        foreach (var removed in fromSet.Except(toSet))
        {
            changes.Add(new ValueChange(path, removed.ToString(), null));
        }
    }

    /// <summary>
    /// Diffs two sets of string keys, emitting added/removed changes.
    /// </summary>
    public static void CompareStringSets(List<ValueChange> changes, string path, IEnumerable<string> from, IEnumerable<string> to)
    {
        var fromSet = from.ToHashSet();
        var toSet = to.ToHashSet();

        foreach (var added in toSet.Except(fromSet))
        {
            changes.Add(new ValueChange(path, null, added));
        }

        foreach (var removed in fromSet.Except(toSet))
        {
            changes.Add(new ValueChange(path, removed, null));
        }
    }

    /// <summary>
    /// Converts an object value to a string for comparison.
    /// </summary>
    public static string? Stringify(object? value) => value switch
    {
        null => null,
        string s => s,
        JsonElement je => je.ToString(),
        _ => value.ToString(),
    };
}
