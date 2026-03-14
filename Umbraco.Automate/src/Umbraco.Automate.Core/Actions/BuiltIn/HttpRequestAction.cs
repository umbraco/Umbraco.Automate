using System.Text;
using System.Text.Json;

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

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpRequestAction"/> class.
    /// </summary>
    public HttpRequestAction(ActionInfrastructure infrastructure, IHttpClientFactory httpClientFactory)
        : base(infrastructure)
    {
        _httpClientFactory = httpClientFactory;
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
            request.Content = new StringContent(settings.Body, Encoding.UTF8, settings.ContentType);
        }

        ApplyHeaders(request, settings.Headers);

        var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        var output = new HttpRequestOutput
        {
            StatusCode = (int)response.StatusCode,
            ResponseBody = body,
            IsSuccess = response.IsSuccessStatusCode,
        };

        return response.IsSuccessStatusCode
            ? ActionResult.Success(output)
            : ActionResult.Failed(
                new HttpRequestException($"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}"),
                StepRunErrorCategory.InvalidResponse);
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
