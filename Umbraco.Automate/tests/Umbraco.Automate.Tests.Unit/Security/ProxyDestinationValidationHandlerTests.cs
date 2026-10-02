using System.Net;
using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Security;

public class ProxyDestinationValidationHandlerTests
{
    private const int MaxRedirects = 5;

    private readonly Dictionary<string, IPAddress[]> _dns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["public.example"] = [IPAddress.Parse("93.184.216.34")],
        ["other.example"] = [IPAddress.Parse("1.1.1.1")],
        ["internal.example"] = [IPAddress.Parse("10.0.0.5")],
        ["mixed.example"] = [IPAddress.Parse("93.184.216.34"), IPAddress.Parse("127.0.0.1")],
        ["bypassed.internal"] = [IPAddress.Parse("10.0.0.6")],
    };

    private readonly List<string> _lookups = [];

    [Fact]
    public async Task SendAsync_ProxiedBlockedHost_RejectedBeforeSend()
    {
        var inner = new FakeInnerHandler();
        using var client = CreateClient(inner);

        var ex = await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("https://internal.example/secret"));

        ex.InnerException.ShouldBeOfType<SsrfException>();
        ex.Message.ShouldBe("Request to 'internal.example' blocked: resolved to a private or reserved address.");
        inner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_ProxiedHostWithAnyBlockedAddress_Rejected()
    {
        var inner = new FakeInnerHandler();
        using var client = CreateClient(inner);

        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("https://mixed.example/"));

        inner.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("http://127.0.0.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[::ffff:0:a00:1]/")]
    public async Task SendAsync_ProxiedBlockedIpLiteral_RejectedWithoutLookup(string url)
    {
        var inner = new FakeInnerHandler();
        using var client = CreateClient(inner);

        var ex = await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync(url));

        ex.InnerException.ShouldBeOfType<SsrfException>();
        inner.Requests.ShouldBeEmpty();
        _lookups.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_ProxiedPublicHost_Sent()
    {
        var inner = new FakeInnerHandler();
        using var client = CreateClient(inner);

        using var response = await client.GetAsync("https://public.example/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.Requests.ShouldBe(["GET https://public.example/"]);
        _lookups.ShouldBe(["public.example"]);
    }

    [Fact]
    public async Task SendAsync_BypassedHost_NoPreCheck()
    {
        // Bypassed hosts connect directly, so the connect-time check applies instead.
        var inner = new FakeInnerHandler();
        using var client = CreateClient(inner, new FakeProxy(bypassHosts: ["bypassed.internal"]));

        using var response = await client.GetAsync("https://bypassed.internal/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.Requests.ShouldBe(["GET https://bypassed.internal/"]);
        _lookups.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_NoProxy_NoPreCheck()
    {
        var inner = new FakeInnerHandler();
        using var client = CreateClient(inner, noProxy: true);

        using var response = await client.GetAsync("https://internal.example/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        _lookups.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_ProxiedRedirectToBlockedHost_Rejected()
    {
        var inner = new FakeInnerHandler();
        inner.Redirect("https://public.example/start", HttpStatusCode.Found, "https://internal.example/secret");
        using var client = CreateClient(inner);

        var ex = await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("https://public.example/start"));

        ex.InnerException.ShouldBeOfType<SsrfException>();
        inner.Requests.ShouldBe(["GET https://public.example/start"]);
    }

    [Fact]
    public async Task SendAsync_ProxiedRedirectToBlockedIpLiteralOnLaterHop_Rejected()
    {
        var inner = new FakeInnerHandler();
        inner.Redirect("https://public.example/a", HttpStatusCode.MovedPermanently, "https://other.example/b");
        inner.Redirect("https://other.example/b", HttpStatusCode.TemporaryRedirect, "https://169.254.169.254/latest");
        using var client = CreateClient(inner);

        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("https://public.example/a"));

        inner.Requests.ShouldBe(["GET https://public.example/a", "GET https://other.example/b"]);
    }

    [Fact]
    public async Task SendAsync_ProxiedRedirectToPublicHost_Followed()
    {
        var inner = new FakeInnerHandler();
        inner.Redirect("https://public.example/start", HttpStatusCode.Found, "/next");
        inner.Redirect("https://public.example/next", HttpStatusCode.PermanentRedirect, "https://other.example/end");
        using var client = CreateClient(inner);

        using var response = await client.GetAsync("https://public.example/start");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.RequestMessage!.RequestUri.ShouldBe(new Uri("https://other.example/end"));
        inner.Requests.ShouldBe([
            "GET https://public.example/start",
            "GET https://public.example/next",
            "GET https://other.example/end",
        ]);
    }

    [Fact]
    public async Task SendAsync_RedirectLimitExceeded_ReturnsLastRedirectResponse()
    {
        var inner = new FakeInnerHandler();
        for (var i = 0; i <= MaxRedirects; i++)
        {
            inner.Redirect($"https://public.example/{i}", HttpStatusCode.Found, $"https://public.example/{i + 1}");
        }

        using var client = CreateClient(inner);

        using var response = await client.GetAsync("https://public.example/0");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        inner.Requests.Count.ShouldBe(MaxRedirects + 1);
    }

    [Fact]
    public async Task SendAsync_NoRedirectMode_ReturnsRedirectWithoutFollowing()
    {
        var inner = new FakeInnerHandler();
        inner.Redirect("https://public.example/start", HttpStatusCode.Found, "https://internal.example/secret");
        using var client = CreateClient(inner, maxRedirects: 0);

        using var response = await client.GetAsync("https://public.example/start");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        inner.Requests.ShouldBe(["GET https://public.example/start"]);
    }

    [Fact]
    public async Task SendAsync_HttpsToHttpRedirect_NotFollowed()
    {
        var inner = new FakeInnerHandler();
        inner.Redirect("https://public.example/start", HttpStatusCode.Found, "http://other.example/");
        using var client = CreateClient(inner);

        using var response = await client.GetAsync("https://public.example/start");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        inner.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.Found, "POST", "GET", false)]
    [InlineData(HttpStatusCode.SeeOther, "PUT", "GET", false)]
    [InlineData(HttpStatusCode.TemporaryRedirect, "POST", "POST", true)]
    [InlineData(HttpStatusCode.PermanentRedirect, "PUT", "PUT", true)]
    public async Task SendAsync_Redirect_AppliesMethodAndBodySemantics(
        HttpStatusCode status,
        string method,
        string expectedMethod,
        bool expectBody)
    {
        var inner = new FakeInnerHandler();
        inner.Redirect("https://public.example/start", status, "https://other.example/end");
        using var client = CreateClient(inner);

        using var request = new HttpRequestMessage(new HttpMethod(method), "https://public.example/start")
        {
            Content = new StringContent("payload"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "token");

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.Requests[^1].ShouldBe($"{expectedMethod} https://other.example/end");
        inner.LastHadBody.ShouldBe(expectBody);
        inner.LastHadAuthorization.ShouldBeFalse();
    }

    private HttpClient CreateClient(
        HttpMessageHandler inner,
        IWebProxy? proxy = null,
        int maxRedirects = MaxRedirects,
        bool noProxy = false)
    {
        var effectiveProxy = noProxy ? null : proxy ?? new FakeProxy();
        var handler = new ProxyDestinationValidationHandler(
            inner,
            () => effectiveProxy,
            (host, _) =>
            {
                _lookups.Add(host);
                return Task.FromResult(_dns.TryGetValue(host, out var addresses) ? addresses : []);
            },
            maxRedirects);

        return new HttpClient(handler);
    }

    private sealed class FakeProxy(string[]? bypassHosts = null) : IWebProxy
    {
        private static readonly Uri ProxyUri = new("http://proxy.corp.example:8080");

        public ICredentials? Credentials { get; set; }

        public Uri? GetProxy(Uri destination) => IsBypassed(destination) ? destination : ProxyUri;

        public bool IsBypassed(Uri host) => bypassHosts?.Contains(host.Host, StringComparer.OrdinalIgnoreCase) == true;
    }

    private sealed class FakeInnerHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Location)> _redirects = [];

        public List<string> Requests { get; } = [];

        public bool LastHadBody { get; private set; }

        public bool LastHadAuthorization { get; private set; }

        public void Redirect(string from, HttpStatusCode status, string location) => _redirects[from] = (status, location);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!.ToString();
            Requests.Add($"{request.Method} {uri}");
            LastHadBody = request.Content is not null;
            LastHadAuthorization = request.Headers.Authorization is not null;

            var response = new HttpResponseMessage(HttpStatusCode.OK) { RequestMessage = request };
            if (_redirects.TryGetValue(uri, out var redirect))
            {
                response.StatusCode = redirect.Status;
                response.Headers.Location = new Uri(redirect.Location, UriKind.RelativeOrAbsolute);
            }

            return Task.FromResult(response);
        }
    }
}
