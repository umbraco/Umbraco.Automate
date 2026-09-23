using Umbraco.Cms.Core.Models;

namespace Umbraco.Automate.Core.Cms;

/// <summary>
/// Downloads a file from a URL and stores it on a media item's upload property.
/// </summary>
public interface IMediaFileDownloader
{
    /// <summary>
    /// Downloads <paramref name="url"/> and stores it on <paramref name="propertyAlias"/> of
    /// <paramref name="media"/>. The media item is not saved — the caller owns that, so the
    /// file and any other changes land in a single save.
    /// </summary>
    /// <param name="media">The media item to store the file on.</param>
    /// <param name="url">The absolute http(s) URL to download.</param>
    /// <param name="propertyAlias">The upload property to write, usually <c>umbracoFile</c>.</param>
    /// <param name="cancellationToken">A token to cancel the download.</param>
    Task<MediaFileDownloadResult> DownloadToPropertyAsync(
        IMedia media,
        string url,
        string propertyAlias,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets the upload property alias written when the caller doesn't specify one.
    /// </summary>
    string DefaultFilePropertyAlias { get; }
}

/// <summary>
/// The outcome of a <see cref="IMediaFileDownloader.DownloadToPropertyAsync"/> call.
/// </summary>
/// <param name="Success">Whether the file was downloaded and stored.</param>
/// <param name="FileName">The file name stored on the property, when successful.</param>
/// <param name="FailureReason">Why the download failed, when unsuccessful.</param>
public readonly record struct MediaFileDownloadResult(bool Success, string? FileName, string? FailureReason)
{
    /// <summary>Creates a successful result for <paramref name="fileName"/>.</summary>
    public static MediaFileDownloadResult Succeeded(string fileName) => new(true, fileName, null);

    /// <summary>Creates a failed result carrying <paramref name="reason"/>.</summary>
    public static MediaFileDownloadResult Failed(string reason) => new(false, null, reason);
}
