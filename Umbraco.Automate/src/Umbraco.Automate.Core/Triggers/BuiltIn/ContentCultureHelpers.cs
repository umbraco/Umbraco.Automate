using Umbraco.Cms.Core.Models;
using Umbraco.Extensions;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Helpers for extracting the per-event culture set from a notification's
/// post-commit <see cref="IContent"/>. The CMS resets dirty state with
/// <c>rememberDirty: true</c> before raising notifications, so each
/// <see cref="ContentCultureInfos"/> entry's <c>WasDirty()</c> tells us
/// which cultures changed during the event.
/// </summary>
internal static class ContentCultureHelpers
{
    /// <summary>
    /// Returns the cultures that were just published, or null for invariant content.
    /// Sourced from <see cref="IContent.PublishCultureInfos"/> entries whose
    /// <c>WasDirty()</c> is true — mirroring how
    /// <c>ContentService.CommitDocumentChangesInternal</c> builds its
    /// <c>culturesPublishing</c> list pre-commit.
    /// </summary>
    public static string[]? GetPublishedCultures(IContent content)
    {
        if (!content.ContentType.VariesByCulture())
        {
            return null;
        }

        return content.PublishCultureInfos?.Values
            .Where(x => x.WasDirty())
            .Select(x => x.Culture)
            .ToArray() ?? Array.Empty<string>();
    }

    /// <summary>
    /// Returns the cultures whose variant data changed during the save, or null
    /// for invariant content. Mirrors the CMS's pre-commit
    /// <c>culturesChanging</c> list.
    /// </summary>
    public static string[]? GetSavedCultures(IContent content)
    {
        if (!content.ContentType.VariesByCulture())
        {
            return null;
        }

        return content.CultureInfos?.Values
            .Where(x => x.WasDirty())
            .Select(x => x.Culture)
            .ToArray() ?? Array.Empty<string>();
    }

    /// <summary>
    /// Returns the cultures that were unpublished when a whole content item is
    /// unpublished (i.e. all currently-published cultures on a variant doc), or
    /// null for invariant content.
    /// </summary>
    public static string[]? GetUnpublishedCultures(IContent content)
    {
        if (!content.ContentType.VariesByCulture())
        {
            return null;
        }

        return content.PublishCultureInfos?.Values
            .Select(x => x.Culture)
            .ToArray() ?? Array.Empty<string>();
    }
}
