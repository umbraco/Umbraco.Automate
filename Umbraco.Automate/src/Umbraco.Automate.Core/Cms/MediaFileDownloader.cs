using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Http;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Core.Cms;

/// <summary>
/// Downloads a file from a URL and stores it on a media item's upload property.
/// <para>
/// A URL is the only file source an automation realistically has — there is no browser on the
/// other end to post a multipart upload — so this is the package's equivalent of the CMS's
/// <see cref="IMediaImportService"/>, which takes a stream from a temporary file the backoffice
/// already received. The store itself goes through the same <c>SetValue</c> extension that
/// <c>IMediaImportService</c> uses, so files land in the media filesystem exactly as an
/// upload would.
/// </para>
/// </summary>
internal sealed class MediaFileDownloader : IMediaFileDownloader
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly MediaFileManager _mediaFileManager;
    private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
    private readonly IOptions<ExecutionOptions> _executionOptions;

    public MediaFileDownloader(
        IHttpClientFactory httpClientFactory,
        MediaFileManager mediaFileManager,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IShortStringHelper shortStringHelper,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        IOptions<ExecutionOptions> executionOptions)
    {
        _httpClientFactory = httpClientFactory;
        _mediaFileManager = mediaFileManager;
        _mediaUrlGenerators = mediaUrlGenerators;
        _shortStringHelper = shortStringHelper;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        _executionOptions = executionOptions;
    }

    /// <inheritdoc />
    public async Task<MediaFileDownloadResult> DownloadToPropertyAsync(
        IMedia media,
        string url,
        string propertyAlias,
        CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return MediaFileDownloadResult.Failed($"'{url}' is not an absolute http(s) URL.");
        }

        var maxBytes = _executionOptions.Value.MaxMediaFileBytes;

        try
        {
            using var client = _httpClientFactory.CreateClient(Constants.HttpClients.Default);

            // ResponseHeadersRead so an oversized file can be rejected from Content-Length
            // without ever pulling the body across the wire.
            using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return MediaFileDownloadResult.Failed(
                    $"Downloading '{url}' returned HTTP {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > maxBytes)
            {
                return MediaFileDownloadResult.Failed(
                    $"'{url}' is {declaredLength} bytes, which exceeds the maximum media file size of {maxBytes} bytes.");
            }

            await using var buffer = await HttpResponseBodyReader.ReadCappedBytesAsync(response.Content, maxBytes, cancellationToken);
            if (buffer is null)
            {
                return MediaFileDownloadResult.Failed(
                    $"'{url}' exceeds the maximum media file size of {maxBytes} bytes.");
            }

            // FileNameStar is the RFC 5987 encoded form and wins where both are sent, being the
            // one that survives non-ASCII names.
            var disposition = response.Content.Headers.ContentDisposition;
            var suggestedFileName = disposition?.FileNameStar ?? disposition?.FileName;

            var fileName = MediaFileNameResolver.Resolve(
                uri,
                media.Name,
                response.Content.Headers.ContentType?.MediaType,
                suggestedFileName);

            media.SetValue(
                _mediaFileManager,
                _mediaUrlGenerators,
                _shortStringHelper,
                _contentTypeBaseServiceProvider,
                propertyAlias,
                fileName,
                buffer);

            return MediaFileDownloadResult.Succeeded(fileName);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SsrfException ssrf)
        {
            return MediaFileDownloadResult.Failed($"Downloading '{url}' was blocked: {ssrf.Message}");
        }
        catch (HttpRequestException ex)
        {
            return MediaFileDownloadResult.Failed($"Downloading '{url}' failed: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return MediaFileDownloadResult.Failed($"Downloading '{url}' timed out.");
        }
    }

    /// <inheritdoc />
    public string DefaultFilePropertyAlias => UmbracoConstants.Conventions.Media.File;
}
