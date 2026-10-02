using Microsoft.AspNetCore.StaticFiles;

namespace Umbraco.Automate.Core.Cms;

/// <summary>
/// Works out the file name to store a downloaded media file under.
/// <para>
/// A URL is not a file picker: it may end in a slash, carry a query string, or name a route
/// with no extension at all. Umbraco's upload property validates the extension against the
/// allowed file types, so a name with no extension is stored but then reads as an invalid
/// file — hence the fall back to the response's content type before giving up.
/// </para>
/// <para>
/// Sanitising the result is deliberately not done here: <c>SetValue</c> already runs the name
/// through <c>IShortStringHelper.CleanStringForSafeFileName</c> before storing it, and doing it
/// twice would only risk the two rules disagreeing.
/// </para>
/// </summary>
internal static class MediaFileNameResolver
{
    /// <summary>
    /// ASP.NET Core's extension-to-content-type table — the same one the CMS uses to serve
    /// back-office graphics. Only ever asked questions in its own direction: whether a name
    /// carries a usable extension, and whether a candidate extension really is the type in hand.
    /// </summary>
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    /// <summary>
    /// Resolves the name to store a download under, in descending order of authority: the name
    /// the server states in <c>Content-Disposition</c>, the name in the URL's path, then
    /// <paramref name="fallbackName"/>. When the chosen name carries no usable extension, one is
    /// appended if <paramref name="contentType"/> can name it.
    /// </summary>
    /// <param name="uri">The URL the file was downloaded from.</param>
    /// <param name="fallbackName">The media item's name, used when nothing else names a file.</param>
    /// <param name="contentType">The response's media type, without parameters.</param>
    /// <param name="suggestedFileName">The <c>Content-Disposition</c> file name, if the server sent one.</param>
    public static string Resolve(Uri uri, string? fallbackName, string? contentType, string? suggestedFileName = null)
    {
        // The server naming the file outright beats inferring one, so Content-Disposition wins.
        // It is remote input, so only its file-name part is taken: a server answering
        // filename="../../web.config" must not reach outside the media folder.
        var candidate = TakeFileName(suggestedFileName?.Trim('"'));

        if (string.IsNullOrWhiteSpace(candidate))
        {
            // LocalPath excludes the query string and is already percent-decoded, so the only
            // thing left to handle is a trailing slash — without the trim, a URL ending in one
            // would name no file at all and fall back unnecessarily.
            candidate = TakeFileName(uri.LocalPath.TrimEnd('/'));
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = string.IsNullOrWhiteSpace(fallbackName) ? "file" : fallbackName;
        }

        // Asking the table whether the name resolves to a known type, rather than
        // Path.HasExtension, which is too loose: it calls the ".2" in "v1.2" an extension and
        // would leave that file with no usable one.
        if (ContentTypes.TryGetContentType(candidate, out _))
        {
            return candidate;
        }

        var extension = ResolveExtension(contentType);
        return extension is null ? candidate : candidate + extension;
    }

    /// <summary>
    /// Takes just the file-name part of a path, tolerating either slash so a remote header
    /// cannot smuggle a directory in. Returns null when nothing is left.
    /// </summary>
    private static string? TakeFileName(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var name = path[(path.LastIndexOfAny(['/', '\\']) + 1)..];
        return string.IsNullOrWhiteSpace(name) || name is "." or ".." ? null : name;
    }

    /// <summary>
    /// Finds the extension for a content type by having the type name it.
    /// <para>
    /// <c>image/jpeg</c> suggests <c>.jpeg</c>, and asking the table confirms that really is
    /// <c>image/jpeg</c>, so the answer verifies itself and no tiebreak is needed. A type that
    /// cannot name itself gets no extension rather than a guess: reversing the table instead
    /// would answer, but answer badly, resolving <c>text/plain</c> to <c>.asm</c>.
    /// </para>
    /// </summary>
    private static string? ResolveExtension(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var slash = contentType.IndexOf('/');
        if (slash >= 0)
        {
            // "image/svg+xml" names ".svg" — the "+xml" is a structuring suffix, not part of it.
            var subtype = contentType[(slash + 1)..];
            var plus = subtype.IndexOf('+');
            if (plus > 0)
            {
                subtype = subtype[..plus];
            }

            var suggested = "." + subtype;
            if (ContentTypes.TryGetContentType("_" + suggested, out var roundTripped)
                && string.Equals(roundTripped, contentType, StringComparison.OrdinalIgnoreCase))
            {
                return suggested;
            }
        }

        return null;
    }
}
