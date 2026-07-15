using Umbraco.Cms.Core.Models;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Helpers for extracting the culture set reported for a content item in a trigger event.
/// <para>
/// Since CMS PR #23313 (closes umbraco/Umbraco-CMS#23288) the content notifications expose the
/// affected cultures per document directly — PublishedCultures / SavedCultures / UnpublishedCultures,
/// keyed by content <c>Key</c>. These helpers prefer that authoritative delta whenever the
/// notification carries an entry for the document.
/// </para>
/// <para>
/// The CMS does not populate a per-item delta in every case — most notably for descendants
/// re-published as a side effect of publishing an ancestor (umbraco/Umbraco-CMS#23288), where the
/// map is null or the document is absent. In those cases these helpers fall back to the
/// change-tracking heuristic (issue #113): prefer the cultures whose state changed on the live
/// instance, then fall back to the full culture set when change tracking has been stripped by a
/// deep clone, so the output is never empty for variant content.
/// </para>
/// <para>
/// Invariant content always returns <c>null</c> (the CMS reports it as the <c>"*"</c> marker; the
/// Automate output contract represents "not culture-specific" as <c>null</c>).
/// </para>
/// </summary>
internal static class ContentCultureHelpers
{
    /// <summary>
    /// Returns the cultures published in this event, or null for invariant content.
    /// Prefers the per-document delta the CMS reports on the notification; falls back to the
    /// change-tracking heuristic when the notification carries no entry for the item.
    /// </summary>
    public static string[]? GetPublishedCultures(
        IContent content,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>? publishedCultures)
    {
        if (!content.ContentType.VariesByCulture())
        {
            return null;
        }

        return TryGetReportedCultures(content, publishedCultures)
            ?? ChangedOrAllCultures(content.PublishCultureInfos);
    }

    /// <summary>
    /// Returns the cultures edited in this save, or null for invariant content.
    /// Prefers the per-document delta the CMS reports on the notification; falls back to the
    /// change-tracking heuristic when the notification carries no entry for the item.
    /// </summary>
    public static string[]? GetSavedCultures(
        IContent content,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>? savedCultures)
    {
        if (!content.ContentType.VariesByCulture())
        {
            return null;
        }

        return TryGetReportedCultures(content, savedCultures)
            ?? ChangedOrAllCultures(content.CultureInfos);
    }

    /// <summary>
    /// Returns the cultures that were unpublished, or null for invariant content.
    /// Prefers the per-document delta the CMS reports on the notification; falls back to all
    /// currently-published cultures when the notification carries no entry for the item.
    /// </summary>
    public static string[]? GetUnpublishedCultures(
        IContent content,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>? unpublishedCultures)
    {
        if (!content.ContentType.VariesByCulture())
        {
            return null;
        }

        return TryGetReportedCultures(content, unpublishedCultures)
            ?? AllCultures(content.PublishCultureInfos);
    }

    // The authoritative per-document delta from the notification, or null when the CMS reported
    // nothing for this document (the caller then applies its change-tracking fallback).
    private static string[]? TryGetReportedCultures(
        IContent content,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<string>>? reportedCultures)
        => reportedCultures is not null && reportedCultures.TryGetValue(content.Key, out var cultures)
            ? cultures.ToArray()
            : null;

    // Fallback heuristic (variant content only — caller has already excluded invariant):
    // prefer the cultures whose state changed on the live instance, else the full set.
    private static string[] ChangedOrAllCultures(ContentCultureInfosCollection? infos)
    {
        var values = infos?.Values;
        if (values is null)
        {
            return Array.Empty<string>();
        }

        var changed = values.Where(x => x.WasDirty()).Select(x => x.Culture).ToArray();
        return changed.Length > 0
            ? changed
            : values.Select(x => x.Culture).ToArray();
    }

    private static string[] AllCultures(ContentCultureInfosCollection? infos)
        => infos?.Values.Select(x => x.Culture).ToArray() ?? Array.Empty<string>();
}
