using Umbraco.Cms.Core.Models;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Helpers for extracting the culture set from a notification's <see cref="IContent"/>.
/// <para>
/// "Cultures changed in this event" can only be read from a <b>live</b> instance's change
/// tracking. Umbraco strips change tracking whenever it deep-clones content — which it does
/// across cache boundaries and when re-fetching descendants for branch/descendant republish —
/// so <c>WasDirty()</c> is permanently <c>false</c> on a cloned instance. Relying on it alone
/// made the culture output collapse to an empty array (issue #113); the same limitation
/// affects the CMS's own <c>ContentNotificationExtensions</c> (e.g. <c>HasPublishedCulture</c>).
/// </para>
/// <para>
/// <see cref="GetPublishedCultures"/> and <see cref="GetSavedCultures"/> therefore prefer the
/// changed cultures (accurate for the common direct publish/save, where the notification
/// carries the live instance) and fall back to the full set when change tracking is
/// unavailable (a cloned instance), so the output is never empty for variant content. The
/// per-item changed-culture delta does not exist anywhere for branch publishes, so the
/// fallback is the best available there. Unpublished reports the full published set — the
/// unpublished culture is removed from the collection, so dirty tracking cannot identify it.
/// </para>
/// </summary>
internal static class ContentCultureHelpers
{
    /// <summary>
    /// Returns the cultures published in this event, or null for invariant content.
    /// Prefers the cultures whose publish state changed (dirty tracking on the live instance);
    /// falls back to all currently-published cultures when change tracking is unavailable
    /// (e.g. the notification carried a deep-cloned instance — see issue #113).
    /// </summary>
    public static string[]? GetPublishedCultures(IContent content)
        => ChangedOrAllCultures(content, content.PublishCultureInfos);

    /// <summary>
    /// Returns the cultures edited in this save, or null for invariant content.
    /// Prefers the cultures whose variant data changed (dirty tracking on the live instance);
    /// falls back to all available cultures when change tracking is unavailable
    /// (e.g. the notification carried a deep-cloned instance — see issue #113).
    /// </summary>
    public static string[]? GetSavedCultures(IContent content)
        => ChangedOrAllCultures(content, content.CultureInfos);

    /// <summary>
    /// Returns the cultures that were published before the item was unpublished
    /// (i.e. all currently-published cultures on a variant doc), or null for invariant content.
    /// Sourced from <see cref="IPublishableContentBase.PublishCultureInfos"/>.
    /// </summary>
    public static string[]? GetUnpublishedCultures(IContent content)
        => AllCultures(content, content.PublishCultureInfos);

    private static string[]? ChangedOrAllCultures(IContent content, ContentCultureInfosCollection? infos)
    {
        if (!content.ContentType.VariesByCulture())
        {
            return null;
        }

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

    private static string[]? AllCultures(IContent content, ContentCultureInfosCollection? infos)
        => content.ContentType.VariesByCulture()
            ? infos?.Values.Select(x => x.Culture).ToArray() ?? Array.Empty<string>()
            : null;
}
