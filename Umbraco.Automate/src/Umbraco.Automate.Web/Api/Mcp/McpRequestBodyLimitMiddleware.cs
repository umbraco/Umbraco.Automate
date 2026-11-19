using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// Caps the size of an MCP request body at <see cref="McpOptions.MaxPayloadBytes"/> before
/// routing, so neither routing nor the MCP handler can read more than that. A tool call's arguments
/// are stored with its run, and the endpoint may be unauthenticated by design, so without a cap any
/// caller could store up to the server's default body limit per call.
/// </summary>
internal sealed class McpRequestBodyLimitMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IOptions<McpOptions> _mcpOptions;

    /// <inheritdoc cref="McpRequestBodyLimitMiddleware"/>
    public McpRequestBodyLimitMiddleware(RequestDelegate next, IOptions<McpOptions> mcpOptions)
    {
        _next = next;
        _mcpOptions = mcpOptions;
    }

    /// <summary>
    /// Rejects a declared oversized body with 413 and caps reads of an undeclared (chunked) one.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var maxPayloadBytes = _mcpOptions.Value.MaxPayloadBytes;
        if (context.Request.ContentLength > maxPayloadBytes)
        {
            await Results.Problem(
                    title: "Payload too large",
                    detail: $"Maximum MCP request size is {maxPayloadBytes} bytes.",
                    statusCode: StatusCodes.Status413PayloadTooLarge)
                .ExecuteAsync(context);
            return;
        }

        // Makes the server fail any read past the limit, for a chunked body with no declared length.
        if (context.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodySizeFeature)
        {
            bodySizeFeature.MaxRequestBodySize = maxPayloadBytes;
        }

        await _next(context);
    }
}
