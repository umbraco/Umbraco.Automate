using System.Net;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Validates the destination of outbound requests that are routed through a proxy, and follows
/// redirects itself so that every hop is validated.
/// </summary>
/// <remarks>
/// <para>
/// When a request goes through a proxy, the connection is made to the proxy, so the connect-time
/// address check in <see cref="SsrfProtectionHandler"/> only ever sees the proxy's address. This
/// handler closes that gap: before a request is sent via the proxy, the destination host is
/// resolved (or its IP literal parsed) and the request is rejected if <em>any</em> resolved address
/// is blocked. Requests the proxy bypasses connect directly and keep relying on the connect-time
/// check.
/// </para>
/// <para>
/// Redirects are followed inside <see cref="SocketsHttpHandler"/>, where an outer handler would
/// only see the first hop, so the inner handler must have automatic redirects disabled and this
/// handler follows them instead, mirroring the .NET redirect semantics: 300/301/302/303/307/308
/// with a <c>Location</c> header, relative locations resolved against the current URI, no
/// https-to-http downgrade, the <c>Authorization</c> header cleared, POST (301/302/300) and any
/// non-GET/HEAD (303) switched to a body-less GET, and the final 3xx returned once the redirect
/// limit is exceeded.
/// </para>
/// <para>
/// There is a residual window between this lookup and the proxy's own lookup of the same host (DNS
/// rebinding); the proxy itself should still deny access to internal networks.
/// </para>
/// </remarks>
internal sealed class ProxyDestinationValidationHandler : DelegatingHandler
{
    private readonly Func<IWebProxy?> _proxyAccessor;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolveHostAsync;
    private readonly int _maxRedirects;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProxyDestinationValidationHandler"/> class.
    /// </summary>
    /// <param name="innerHandler">The inner handler. Must not follow redirects itself.</param>
    /// <param name="proxyAccessor">Returns the effective proxy, evaluated per request.</param>
    /// <param name="resolveHostAsync">Resolves a host name to its addresses.</param>
    /// <param name="maxRedirects">The maximum number of redirects to follow; 0 follows none.</param>
    public ProxyDestinationValidationHandler(
        HttpMessageHandler innerHandler,
        Func<IWebProxy?> proxyAccessor,
        Func<string, CancellationToken, Task<IPAddress[]>> resolveHostAsync,
        int maxRedirects)
        : base(innerHandler)
    {
        _proxyAccessor = proxyAccessor;
        _resolveHostAsync = resolveHostAsync;
        _maxRedirects = maxRedirects;
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await EnsureDestinationAllowedAsync(request.RequestUri!, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);

        var redirectCount = 0;
        while (GetRedirectUri(request.RequestUri!, response) is { } redirectUri)
        {
            redirectCount++;
            if (redirectCount > _maxRedirects)
            {
                // Same as .NET: past the limit, the last 3xx response is returned as-is.
                break;
            }

            await EnsureDestinationAllowedAsync(redirectUri, cancellationToken);

            var statusCode = response.StatusCode;
            response.Dispose();

            request.Headers.Authorization = null;
            if (RequiresChangeToGet(statusCode, request.Method))
            {
                request.Method = HttpMethod.Get;
                request.Content = null;
                if (request.Headers.TransferEncodingChunked == true)
                {
                    request.Headers.TransferEncodingChunked = false;
                }
            }

            request.RequestUri = redirectUri;
            response = await base.SendAsync(request, cancellationToken);
        }

        return response;
    }

    private async Task EnsureDestinationAllowedAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (!UsesProxy(uri))
        {
            // Direct connection — the connect-time check validates the actual address.
            return;
        }

        var host = uri.IdnHost.Trim('[', ']');
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await _resolveHostAsync(host, cancellationToken);

        try
        {
            SsrfProtectionHandler.EnsureAllowed(host, addresses);
        }
        catch (SsrfException ex)
        {
            // Same shape as a connect-time rejection, so callers handle both identically.
            throw new HttpRequestException(HttpRequestError.ConnectionError, ex.Message, ex);
        }
    }

    private bool UsesProxy(Uri uri)
    {
        var proxy = _proxyAccessor();
        if (proxy is null || proxy.IsBypassed(uri))
        {
            return false;
        }

        var proxyUri = proxy.GetProxy(uri);
        return proxyUri is not null && proxyUri != uri;
    }

    private static Uri? GetRedirectUri(Uri requestUri, HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.MultipleChoices
            or HttpStatusCode.MovedPermanently
            or HttpStatusCode.Found
            or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect))
        {
            return null;
        }

        var location = response.Headers.Location;
        if (location is null)
        {
            return null;
        }

        if (!location.IsAbsoluteUri)
        {
            location = new Uri(requestUri, location);
        }

        // Carry the original fragment when the redirect does not specify one.
        if (string.IsNullOrEmpty(location.Fragment) && !string.IsNullOrEmpty(requestUri.Fragment))
        {
            location = new UriBuilder(location) { Fragment = requestUri.Fragment.TrimStart('#') }.Uri;
        }

        if (location.Scheme is not ("http" or "https"))
        {
            return null;
        }

        // Never follow a redirect from a secure to a non-secure scheme.
        if (requestUri.Scheme == Uri.UriSchemeHttps && location.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        return location;
    }

    private static bool RequiresChangeToGet(HttpStatusCode statusCode, HttpMethod method) => statusCode switch
    {
        HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.MultipleChoices
            => method == HttpMethod.Post,
        HttpStatusCode.SeeOther => method != HttpMethod.Get && method != HttpMethod.Head,
        _ => false,
    };
}
