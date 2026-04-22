namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Shared predicate for content triggers that expose a <c>ContentTypes</c> setting —
/// a CSV of document-type GUIDs produced by the <c>DocumentTypePicker</c> property editor.
/// </summary>
internal static class ContentTypesFilter
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="contentTypeKey"/> matches the CSV filter.
    /// An empty or null filter matches any content type.
    /// </summary>
    /// <param name="contentTypeKey">The content item's content-type key.</param>
    /// <param name="contentTypesCsv">The filter value from the automation's trigger settings.</param>
    public static bool Matches(Guid? contentTypeKey, string? contentTypesCsv)
    {
        if (string.IsNullOrWhiteSpace(contentTypesCsv))
        {
            return true;
        }

        if (contentTypeKey is null)
        {
            // Filter is set but the event has no content type — can't match.
            return false;
        }

        foreach (var part in contentTypesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var key) && key == contentTypeKey.Value)
            {
                return true;
            }
        }

        return false;
    }
}
