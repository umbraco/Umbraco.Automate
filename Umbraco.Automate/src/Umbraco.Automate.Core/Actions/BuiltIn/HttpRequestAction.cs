using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that makes an HTTP request to an external URL.
/// </summary>
[Action("umbracoAutomate.httpRequest", "HTTP Request",
    Description = "Sends an HTTP request to an external URL.",
    Group = "Core",
    Icon = "icon-cloud-upload")]
public sealed class HttpRequestAction : ActionBase<HttpRequestSettings, HttpRequestOutput>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<ExecutionOptions> _executionOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestAction"/> class.
    /// </summary>
    public HttpRequestAction(
        ActionInfrastructure infrastructure,
        IHttpClientFactory httpClientFactory,
        IOptions<ExecutionOptions> executionOptions)
        : base(infrastructure)
    {
        _httpClientFactory = httpClientFactory;
        _executionOptions = executionOptions;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<HttpRequestSettings>();

        if (string.IsNullOrWhiteSpace(settings.Url))
        {
            return ActionResult.Failed(
                new ArgumentException("URL is required."),
                StepRunErrorCategory.Validation);
        }

        using var client = _httpClientFactory.CreateClient("UmbracoAutomate");
        using var request = new HttpRequestMessage(ParseMethod(settings.Method), settings.Url);

        if (!string.IsNullOrWhiteSpace(settings.Body) && HasBody(settings.Method))
        {
            // Encode the body as UTF-8, but set the Content-Type header from the configured
            // value verbatim rather than letting StringContent append "; charset=utf-8".
            // Some webhook receivers (e.g. Slack, Discord) reject the charset parameter and
            // treat the request as if it had no body.
            request.Content = new StringContent(settings.Body, Encoding.UTF8);
            if (!string.IsNullOrWhiteSpace(settings.ContentType)
                && MediaTypeHeaderValue.TryParse(settings.ContentType, out var contentType))
            {
                request.Content.Headers.ContentType = contentType;
            }
        }

        ApplyHeaders(request, settings.Headers);

        var maxBodyBytes = _executionOptions.Value.MaxHttpResponseBodyBytes;

        // Stream the response so an oversized body can be rejected from the Content-Length
        // header — or while reading when the server doesn't declare one — without ever
        // buffering the whole payload first.
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > maxBodyBytes)
        {
            return ResponseTooLarge(settings.Url, declaredLength, maxBodyBytes);
        }

        var body = await ReadBodyCappedAsync(response.Content, maxBodyBytes, cancellationToken);
        if (body is null)
        {
            return ResponseTooLarge(settings.Url, actualBytes: null, maxBodyBytes);
        }

        var output = new HttpRequestOutput
        {
            StatusCode = (int)response.StatusCode,
            ResponseBody = body,
            IsSuccess = response.IsSuccessStatusCode,
        };

        return response.IsSuccessStatusCode
            ? Success(output)
            : ActionResult.Failed(
                new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"),
                StepRunErrorCategory.InvalidResponse);
    }

    /// <summary>
    /// Reads the response body up to <paramref name="maxBytes"/>, returning <c>null</c> when
    /// the body turns out to be larger (cap enforced while streaming, so at most one chunk
    /// past the limit is ever buffered).
    /// </summary>
    private static async Task<string?> ReadBodyCappedAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return ResolveEncoding(content.Headers.ContentType?.CharSet).GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private static ActionResult ResponseTooLarge(string? url, long? actualBytes, long maxBytes)
    {
        var size = actualBytes is { } bytes ? $"is {bytes} bytes, which exceeds" : "exceeds";

        // ConfigurationError rather than InvalidResponse: the outcome is deterministic (the
        // endpoint returns the same oversized payload every time), so the terminal category
        // stops WorkflowCore re-downloading megabytes on every retry attempt.
        return ActionResult.Failed(
            new HttpRequestException(
                $"The HTTP response from '{url}' {size} the maximum allowed response body size of {maxBytes} bytes. " +
                "Filter or paginate the request so it returns less data, or increase the " +
                "'Umbraco:Automate:Execution:MaxHttpResponseBodyBytes' setting if the automation genuinely needs a larger response."),
            StepRunErrorCategory.ConfigurationError);
    }

    private static HttpMethod ParseMethod(string? method)
        => method?.ToUpperInvariant() switch
        {
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            "DELETE" => HttpMethod.Delete,
            "PATCH" => HttpMethod.Patch,
            "HEAD" => HttpMethod.Head,
            _ => HttpMethod.Get,
        };

    private static bool HasBody(string? method)
        => method?.ToUpperInvariant() is "POST" or "PUT" or "PATCH";

    private static void ApplyHeaders(HttpRequestMessage request, string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return;
        }

        try
        {
            var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
            if (headers is null)
            {
                return;
            }

            foreach (var (key, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(key, value);
            }
        }
        catch (JsonException)
        {
            // Ignore malformed headers JSON — don't fail the whole action for optional config.
        }
    }
}
