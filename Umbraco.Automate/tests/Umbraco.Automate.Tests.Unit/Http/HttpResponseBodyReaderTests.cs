using System.Net.Http.Headers;
using System.Text;
using Shouldly;
using Umbraco.Automate.Core.Http;

namespace Umbraco.Automate.Tests.Unit.Http;

public class HttpResponseBodyReaderTests
{
    [Fact]
    public async Task ReadCappedAsync_BodyUnderLimit_ReturnsBody()
    {
        using var content = new StringContent("hello");

        var body = await HttpResponseBodyReader.ReadCappedAsync(content, maxBytes: 1024, CancellationToken.None);

        body.ShouldBe("hello");
    }

    [Fact]
    public async Task ReadCappedAsync_BodyAtLimit_ReturnsBody()
    {
        var text = new string('a', 1024);
        using var content = new StringContent(text);

        var body = await HttpResponseBodyReader.ReadCappedAsync(content, maxBytes: 1024, CancellationToken.None);

        body.ShouldBe(text);
    }

    [Fact]
    public async Task ReadCappedAsync_BodyOverLimit_ReturnsNull()
    {
        using var content = new StringContent(new string('a', 2000));

        var body = await HttpResponseBodyReader.ReadCappedAsync(content, maxBytes: 1024, CancellationToken.None);

        body.ShouldBeNull();
    }

    [Fact]
    public async Task ReadCappedAsync_HonoursCharsetFromContentType()
    {
        var bytes = Encoding.Unicode.GetBytes("héllo");
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain") { CharSet = "utf-16" };

        var body = await HttpResponseBodyReader.ReadCappedAsync(content, maxBytes: 1024, CancellationToken.None);

        body.ShouldBe("héllo");
    }

    [Fact]
    public async Task ReadCappedAsync_UnknownLengthOverLimit_ReturnsNullWhileStreaming()
    {
        using var content = new UnknownLengthContent(2000);

        var body = await HttpResponseBodyReader.ReadCappedAsync(content, maxBytes: 1024, CancellationToken.None);

        body.ShouldBeNull();
    }

    private sealed class UnknownLengthContent : HttpContent
    {
        private readonly byte[] _bytes;

        public UnknownLengthContent(int size)
        {
            _bytes = new byte[size];
            Array.Fill(_bytes, (byte)'a');
        }

        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
            => stream.WriteAsync(_bytes).AsTask();

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
