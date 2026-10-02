using System.Net;
using Microsoft.Extensions.Options;
using Moq.Protected;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Models;

namespace Umbraco.Automate.Tests.Unit.Cms;

public class MediaFileDownloaderTests
{
    private const long MaxBytes = 1024;

    // Strict, with no setups: any attempt to read or write the media item throws, so every
    // failure-path test also proves nothing was stored on it.
    private readonly Mock<IMedia> _media = new(MockBehavior.Strict);

    [Theory]
    [InlineData("ftp://example.com/file.jpg")]
    [InlineData("file:///c:/secrets.txt")]
    [InlineData("/relative/file.jpg")]
    [InlineData("not a url")]
    public async Task DownloadToPropertyAsync_NotAnAbsoluteHttpUrl_FailsWithoutRequesting(string url)
    {
        var handler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        var downloader = CreateDownloader(handler.Object);

        var result = await downloader.DownloadToPropertyAsync(_media.Object, url, "umbracoFile", CancellationToken.None);

        AssertFailed(result, "is not an absolute http(s) URL");
        handler.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task DownloadToPropertyAsync_NonSuccessStatus_Fails(HttpStatusCode statusCode)
    {
        var downloader = CreateDownloader(Respond(new HttpResponseMessage(statusCode)
        {
            Content = new ByteArrayContent([1, 2, 3]),
        }));

        var result = await downloader.DownloadToPropertyAsync(
            _media.Object, "https://example.com/photo.jpg", "umbracoFile", CancellationToken.None);

        AssertFailed(result, $"HTTP {(int)statusCode}");
    }

    [Fact]
    public async Task DownloadToPropertyAsync_ContentLengthOverCap_FailsWithoutReadingBody()
    {
        var content = new DeclaredLengthContent(declaredLength: MaxBytes + 1);
        var downloader = CreateDownloader(Respond(new HttpResponseMessage(HttpStatusCode.OK) { Content = content }));

        var result = await downloader.DownloadToPropertyAsync(
            _media.Object, "https://example.com/photo.jpg", "umbracoFile", CancellationToken.None);

        AssertFailed(result, $"is {MaxBytes + 1} bytes");
        content.BodyRead.ShouldBeFalse();
    }

    [Fact]
    public async Task DownloadToPropertyAsync_BodyOverCapWithoutContentLength_Fails()
    {
        var downloader = CreateDownloader(Respond(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new UnknownLengthContent((int)MaxBytes * 2),
        }));

        var result = await downloader.DownloadToPropertyAsync(
            _media.Object, "https://example.com/photo.jpg", "umbracoFile", CancellationToken.None);

        AssertFailed(result, "exceeds the maximum media file size");
    }

    [Fact]
    public async Task DownloadToPropertyAsync_SsrfBlocked_FailsAsBlocked()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("connect failed", new SsrfException("resolved to a private address")));
        var downloader = CreateDownloader(handler.Object);

        var result = await downloader.DownloadToPropertyAsync(
            _media.Object, "http://internal.example/photo.jpg", "umbracoFile", CancellationToken.None);

        AssertFailed(result, "was blocked: resolved to a private address");
    }

    [Fact]
    public async Task DownloadToPropertyAsync_OtherHttpRequestException_FailsWithMessage()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("No such host is known."));
        var downloader = CreateDownloader(handler.Object);

        var result = await downloader.DownloadToPropertyAsync(
            _media.Object, "https://nowhere.example/photo.jpg", "umbracoFile", CancellationToken.None);

        AssertFailed(result, "failed: No such host is known.");
    }

    [Fact]
    public async Task DownloadToPropertyAsync_ClientTimeout_FailsAsTimedOut()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (_, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        var downloader = CreateDownloader(handler.Object, TimeSpan.FromMilliseconds(50));

        var result = await downloader.DownloadToPropertyAsync(
            _media.Object, "https://slow.example/photo.jpg", "umbracoFile", CancellationToken.None);

        AssertFailed(result, "timed out");
    }

    [Fact]
    public async Task DownloadToPropertyAsync_CallerCancels_PropagatesCancellation()
    {
        // Only the client's own timeout is reported as a failed result; a caller-requested
        // cancellation must still surface as cancellation so the run can stop.
        using var cts = new CancellationTokenSource();
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns<HttpRequestMessage, CancellationToken>(async (_, ct) =>
            {
                await cts.CancelAsync();
                await Task.Delay(Timeout.Infinite, ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            });
        var downloader = CreateDownloader(handler.Object);

        await Should.ThrowAsync<OperationCanceledException>(() => downloader.DownloadToPropertyAsync(
            _media.Object, "https://slow.example/photo.jpg", "umbracoFile", cts.Token));
    }

    private void AssertFailed(MediaFileDownloadResult result, string reasonFragment)
    {
        result.Success.ShouldBeFalse();
        result.FileName.ShouldBeNull();
        result.FailureReason.ShouldNotBeNull();
        result.FailureReason.ShouldContain(reasonFragment);
        _media.Verify(
            m => m.SetValue(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<string?>(), It.IsAny<string?>()),
            Times.Never);
    }

    private static HttpMessageHandler Respond(HttpResponseMessage response)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        return handler.Object;
    }

    private static MediaFileDownloader CreateDownloader(HttpMessageHandler handler, TimeSpan? timeout = null)
    {
        var client = new HttpClient(handler);
        if (timeout is { } t)
        {
            client.Timeout = t;
        }

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        // The media filesystem collaborators are only touched once a file has been downloaded
        // and is being stored; none of the paths under test get that far.
        return new MediaFileDownloader(
            factory.Object,
            mediaFileManager: null!,
            mediaUrlGenerators: null!,
            shortStringHelper: null!,
            contentTypeBaseServiceProvider: null!,
            Options.Create(new ExecutionOptions { MaxMediaFileBytes = MaxBytes }));
    }

    /// <summary>Declares a Content-Length but records whether anything tried to read the body.</summary>
    private sealed class DeclaredLengthContent : HttpContent
    {
        private readonly long _declaredLength;

        public DeclaredLengthContent(long declaredLength)
        {
            _declaredLength = declaredLength;
            Headers.ContentLength = declaredLength;
        }

        public bool BodyRead { get; private set; }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            BodyRead = true;
            return stream.WriteAsync(new byte[_declaredLength]).AsTask();
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _declaredLength;
            return true;
        }
    }

    /// <summary>A body with no Content-Length, so only the streaming cap can reject it.</summary>
    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnknownLengthContent(int size) => _bytes = new byte[size];

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
