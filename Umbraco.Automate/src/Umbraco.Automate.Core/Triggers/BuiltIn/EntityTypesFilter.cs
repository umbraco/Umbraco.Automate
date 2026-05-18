namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Shared predicate for entity triggers that expose a type-filter setting —
/// a CSV of type GUIDs produced by the <c>DocumentTypePicker</c> or
/// <c>MediaTypePicker</c> property editor.
/// </summary>
internal static class EntityTypesFilter
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="entityTypeKey"/> matches the CSV filter.
    /// An empty or null filter matches any entity type.
    /// </summary>
    /// <param name="entityTypeKey">The entity's content-type or media-type key.</param>
    /// <param name="entityTypesCsv">The filter value from the automation's trigger settings.</param>
    public static bool Matches(Guid? entityTypeKey, string? entityTypesCsv)
    {
        if (string.IsNullOrWhiteSpace(entityTypesCsv))
        {
            return true;
        }

        if (entityTypeKey is null)
        {
            // Filter is set but the event has no entity type — can't match.
            return false;
        }

        foreach (var part in entityTypesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var key) && key == entityTypeKey.Value)
            {
                return true;
            }
        }

        return false;
    }
}
