namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Shared predicate for content triggers that expose a "Published by" filter setting —
/// a <see cref="PublishedByFilter"/> value stored as a string by the dropdown editor.
/// </summary>
internal static class PublisherKindFilter
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="publisherKind"/> matches the filter.
    /// A missing or unparseable filter matches any publisher.
    /// </summary>
    /// <param name="publisherKind">The event's <see cref="ContentPublisherKind"/> classification, or <c>null</c> when unknown.</param>
    /// <param name="publishedByFilter">The filter value from the automation's trigger settings.</param>
    public static bool Matches(string? publisherKind, string? publishedByFilter)
    {
        var filter = Enum.TryParse<PublishedByFilter>(publishedByFilter, ignoreCase: true, out var value)
            ? value
            : PublishedByFilter.Anyone;

        if (filter == PublishedByFilter.Anyone)
        {
            return true;
        }

        if (publisherKind is null)
        {
            // Filter is set but the publisher couldn't be classified — can't match.
            return false;
        }

        return filter switch
        {
            PublishedByFilter.User => publisherKind == ContentPublisherKind.User,
            PublishedByFilter.System => publisherKind is ContentPublisherKind.System or ContentPublisherKind.Api,
            _ => true,
        };
    }
}
