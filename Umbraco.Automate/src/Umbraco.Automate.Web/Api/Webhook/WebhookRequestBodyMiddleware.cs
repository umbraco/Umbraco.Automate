using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;

namespace Umbraco.Automate.Web.Api.Webhook;

/// <summary>
/// Prepares a webhook request body before routing: enforces the payload limit and buffers the body.
/// </summary>
/// <remarks>
/// Umbraco's endpoint matching reads the form of a form-encoded request (looking for a <c>ufprt</c>
/// token), which consumes the stream before the webhook endpoint runs. Buffering lets the endpoint
/// rewind and read the raw body. The payload limit is applied here, before anything reads the body,
/// so neither that form read nor the buffer can take in more than <see cref="WebhookOptions.MaxPayloadBytes"/>.
/// </remarks>
internal sealed class WebhookRequestBodyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<WebhookOptions> _webhookOptions;

    /// <inheritdoc cref="WebhookRequestBodyMiddleware"/>
    public WebhookRequestBodyMiddleware(RequestDelegate next, IOptions<WebhookOptions> webhookOptions)
    {
        _next = next;
        _webhookOptions = webhookOptions;
    }

    /// <summary>
    /// Rejects a declared oversized body with 413, caps reads of an undeclared (chunked) one, and buffers the rest.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var maxPayloadBytes = _webhookOptions.Value.MaxPayloadBytes;
        if (context.Request.ContentLength > maxPayloadBytes)
        {
            await WritePayloadTooLargeAsync(context, maxPayloadBytes);
            return;
        }

        // Makes the server fail any read past the limit, including one made while routing, so the
        // buffer below never holds more than the limit either.
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySizeFeature)
        {
            bodySizeFeature.MaxRequestBodySize = maxPayloadBytes;
        }

        context.Request.EnableBuffering();

        await _next(context);
    }

    // Matches the 413 the webhook endpoint itself returns.
    private static Task WritePayloadTooLargeAsync(HttpContext context, long maxPayloadBytes)
        => Results.Problem(
                title: "Payload too large",
                detail: $"Maximum webhook payload size is {maxPayloadBytes} bytes.",
                statusCode: StatusCodes.Status413PayloadTooLarge)
            .ExecuteAsync(context);
}
