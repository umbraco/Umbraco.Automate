using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Web.Api.Webhook;

namespace Umbraco.Automate.Tests.Unit.Webhook;

public class WebhookRequestBodyMiddlewareTests
{
    private const long MaxPayloadBytes = 1024;

    [Fact]
    public async Task InvokeAsync_DeclaredBodyOverTheLimit_Returns413WithoutRunningTheRest()
    {
        var context = CreateContext(contentLength: MaxPayloadBytes + 1);
        var nextCalled = false;
        var middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status413PayloadTooLarge);
        nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_CapsServerReadsAtTheLimit()
    {
        // A chunked body declares no length, so the server has to enforce the limit as it reads,
        // including the form read Umbraco's routing makes.
        var bodySize = new FakeMaxRequestBodySizeFeature();
        var context = CreateContext(contentLength: null);
        context.Features.Set<IHttpMaxRequestBodySizeFeature>(bodySize);

        await CreateMiddleware(_ => Task.CompletedTask).InvokeAsync(context);

        bodySize.MaxRequestBodySize.ShouldBe(MaxPayloadBytes);
    }

    [Fact]
    public async Task InvokeAsync_BuffersTheBodySoTheEndpointCanRewindIt()
    {
        var context = CreateContext(contentLength: 3);
        context.Request.Body = new NonSeekableStream("a=1"u8.ToArray());
        var bodySeekableInNext = false;

        await CreateMiddleware(c =>
        {
            bodySeekableInNext = c.Request.Body.CanSeek;
            return Task.CompletedTask;
        }).InvokeAsync(context);

        bodySeekableInNext.ShouldBeTrue();
    }

    private static WebhookRequestBodyMiddleware CreateMiddleware(RequestDelegate next)
        => new(next, Options.Create(new WebhookOptions { MaxPayloadBytes = MaxPayloadBytes }));

    private static DefaultHttpContext CreateContext(long? contentLength)
    {
        var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = "POST";
        context.Request.ContentLength = contentLength;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class FakeMaxRequestBodySizeFeature : IHttpMaxRequestBodySizeFeature
    {
        public bool IsReadOnly => false;

        public long? MaxRequestBodySize { get; set; } = 30_000_000;
    }

    private sealed class NonSeekableStream(byte[] content) : MemoryStream(content)
    {
        public override bool CanSeek => false;
    }
}
