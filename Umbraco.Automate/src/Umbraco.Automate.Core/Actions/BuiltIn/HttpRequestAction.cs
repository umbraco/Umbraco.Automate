using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Http;

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

        using var client = _httpClientFactory.CreateClient(Constants.HttpClients.Default);
        using var request = new HttpRequestMessage(ParseMethod(settings.Method), settings.Url);

        if (HasBody(settings.Method))
        {
            if (settings.BodyMode == HttpRequestBodyMode.Form)
            {
                // FormUrlEncodedContent sets "application/x-www-form-urlencoded" itself, so a
                // form post no longer requires the author to also get Content-Type right.
                request.Content = new FormUrlEncodedContent(
                    settings.FormFields
                        .Where(f => !string.IsNullOrWhiteSpace(f.Key))
                        .Select(f => new KeyValuePair<string, string>(f.Key, f.Value ?? string.Empty)));
            }
            else if (!string.IsNullOrWhiteSpace(settings.Body))
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
        }

        if (ApplyHeaders(request, settings.Headers) is { } headerFailure)
        {
            return headerFailure;
        }

        var maxBodyBytes = _executionOptions.Value.MaxHttpResponseBodyBytes;

        // Captured before sending: a followed redirect rewrites the request's method and URL.
        var requestedUri = request.RequestUri;
        var requestLine = $"{request.Method} {ActionLogFormat.Url(requestedUri)}";
        var started = Stopwatch.GetTimestamp();

        // Stream the response so an oversized body can be rejected from the Content-Length
        // header — or while reading when the server doesn't declare one — without ever
        // buffering the whole payload first.
        using var response = await SendAsync(client, request, context, requestLine, started, cancellationToken);

        // Redirects are followed inside the handler, which leaves the final address on the request.
        if (response.RequestMessage?.RequestUri is { } finalUri && finalUri != requestedUri)
        {
            context.LogInfo($"Redirected to {ActionLogFormat.Url(finalUri)}");
        }

        var statusLine = $"{requestLine} → {(int)response.StatusCode} {response.ReasonPhrase}";

        if (response.Content.Headers.ContentLength is { } declaredLength && declaredLength > maxBodyBytes)
        {
            context.LogError(
                $"{statusLine} in {ActionLogFormat.Elapsed(Stopwatch.GetElapsedTime(started))}, but the response body "
                + $"({ActionLogFormat.Bytes(declaredLength)}) exceeds the {ActionLogFormat.Bytes(maxBodyBytes)} limit");
            return ResponseTooLarge(settings.Url, declaredLength, maxBodyBytes);
        }

        var body = await HttpResponseBodyReader.ReadCappedAsync(response.Content, maxBodyBytes, cancellationToken);
        var elapsed = ActionLogFormat.Elapsed(Stopwatch.GetElapsedTime(started));

        if (body is null)
        {
            context.LogError(
                $"{statusLine} in {elapsed}, but the response body exceeds the {ActionLogFormat.Bytes(maxBodyBytes)} limit");
            return ResponseTooLarge(settings.Url, actualBytes: null, maxBodyBytes);
        }

        context.Log(
            response.IsSuccessStatusCode ? ActionLogLevel.Info : ActionLogLevel.Error,
            $"{statusLine} in {elapsed} ({ActionLogFormat.Bytes(Encoding.UTF8.GetByteCount(body))})");

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
    /// Sends the request, recording a failed attempt in the run log before rethrowing so the
    /// pipeline still categorises the exception as it always has.
    /// </summary>
    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpRequestMessage request,
        ActionContext context,
        string requestLine,
        long started,
        CancellationToken cancellationToken)
    {
        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            var reason = ex is OperationCanceledException ? "timed out or was cancelled" : $"failed: {ex.Message}";
            context.LogError($"{requestLine} {reason} after {ActionLogFormat.Elapsed(Stopwatch.GetElapsedTime(started))}");
            throw;
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

    /// <summary>
    /// Applies the configured header rows to the request, returning a failed
    /// <see cref="ActionResult"/> when a row cannot be applied and null when all of them were.
    /// </summary>
    /// <remarks>
    /// A header the author configured but that never reaches the wire is a silent security
    /// problem — that is how the old JSON blob lost an <c>Authorization</c> header to a typo —
    /// so an unusable row fails the step as a validation error rather than being dropped.
    /// Entirely blank rows are ignored: the key/value editor leaves one behind whenever a row
    /// is added and not filled in.
    /// </remarks>
    private static ActionResult? ApplyHeaders(HttpRequestMessage request, IList<HttpRequestKeyValue>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        foreach (var header in headers)
        {
            var value = header.Value ?? string.Empty;

            if (string.IsNullOrWhiteSpace(header.Key))
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                return HeaderFailure("A header value was configured without a header name.");
            }

            if (request.Headers.TryAddWithoutValidation(header.Key, value))
            {
                continue;
            }

            // Content headers (Content-Type, Content-Disposition, ...) are rejected by the
            // request header collection and belong on the body instead. Replace rather than add
            // so an explicitly configured Content-Type wins over the one the body set.
            if (request.Content is not null)
            {
                request.Content.Headers.Remove(header.Key);
                if (request.Content.Headers.TryAddWithoutValidation(header.Key, value))
                {
                    continue;
                }
            }

            return HeaderFailure(
                $"The header '{header.Key}' could not be applied to the request. Check the header "
                + "name for invalid characters, and note that content headers such as Content-Type "
                + "only apply to a request that sends a body.");
        }

        return null;
    }

    private static ActionResult HeaderFailure(string message)
        => ActionResult.Failed(new ArgumentException(message), StepRunErrorCategory.Validation);
}
