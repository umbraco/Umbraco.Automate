namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Shared predicate for triggers that expose a "match if entity belongs to any of these
/// groups" filter — a CSV of group GUIDs produced by a group-picker property editor.
/// Used by member triggers (member groups) and user triggers (user groups), which both
/// store and emit values as <c>Guid</c>s.
/// </summary>
internal static class GroupKeysFilter
{
    /// <summary>
    /// Returns <c>true</c> when the entity belongs to any of the configured groups.
    /// An empty or null filter matches any entity.
    /// </summary>
    /// <param name="entityGroupKeys">The keys of the groups the entity belongs to.</param>
    /// <param name="groupKeysCsv">The filter value from the automation's trigger settings.</param>
    public static bool Matches(IReadOnlyList<Guid>? entityGroupKeys, string? groupKeysCsv)
    {
        if (string.IsNullOrWhiteSpace(groupKeysCsv))
        {
            return true;
        }

        if (entityGroupKeys is null || entityGroupKeys.Count == 0)
        {
            // Filter is set but the entity is in no groups — can't match.
            return false;
        }

        foreach (var part in groupKeysCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var allowed))
            {
                continue;
            }

            foreach (var entityGroupKey in entityGroupKeys)
            {
                if (entityGroupKey == allowed)
                {
                    return true;
                }
            }
        }

        return false;
    }
}
