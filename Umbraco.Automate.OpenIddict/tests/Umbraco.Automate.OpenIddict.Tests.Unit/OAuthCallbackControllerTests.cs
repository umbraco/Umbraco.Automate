using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client.AspNetCore;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Automate.OpenIddict.Controllers;
using Umbraco.Automate.OpenIddict.Credentials;
using static OpenIddict.Client.AspNetCore.OpenIddictClientAspNetCoreConstants;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

/// <summary>
/// Regression tests for the provider-name case mismatch: the persisted <see cref="OAuthCredentials.Provider"/>
/// must come from the original-case value OpenIddict round-trips on <see cref="AuthenticationProperties"/>,
/// not from the always-lowercased <c>{provider}</c> callback route segment. Using the lowercased route
/// segment causes the later, case-sensitive registration lookup in token refresh to fail silently.
/// </summary>
public class OAuthCallbackControllerTests
{
    private readonly Mock<IOAuthCredentialsService> _credentialsService = new();
    private readonly Mock<IAuthenticationService> _authenticationService = new();
    private readonly EphemeralDataProtectionProvider _dataProtectionProvider = new();
    private readonly OAuthCallbackController _controller;

    public OAuthCallbackControllerTests()
    {
        _controller = new OAuthCallbackController(_credentialsService.Object, _dataProtectionProvider, TimeProvider.System);

        var services = new ServiceCollection();
        services.AddSingleton(_authenticationService.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() },
        };
    }

    [Fact]
    public async Task Callback_PersistsProviderName_WithOriginalCaseFromProperties_NotLowercasedRouteSegment()
    {
        // The {provider} route segment is always lowercase by convention (see
        // OpenIddictClientCredentialsConfigurator), so a registration named "GitHub" redirects
        // back through .../callback/github even though the registration itself is "GitHub".
        const string routeSegment = "github";
        const string registeredCaseProviderName = "GitHub";

        SetUpSuccessfulAuthentication(registeredCaseProviderName);

        OAuthCredentials? saved = null;
        _credentialsService
            .Setup(s => s.CreateCredentialsAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>()))
            .Callback<OAuthCredentials, CancellationToken>((c, _) => saved = c)
            .ReturnsAsync((OAuthCredentials c, CancellationToken _) => c);

        await _controller.Callback(routeSegment);

        saved.ShouldNotBeNull();
        saved.Provider.ShouldBe(registeredCaseProviderName);
    }

    [Fact]
    public async Task Callback_FallsBackToRouteSegment_WhenPropertiesProviderNameMissing()
    {
        const string routeSegment = "github";

        SetUpSuccessfulAuthentication(providerName: null);

        OAuthCredentials? saved = null;
        _credentialsService
            .Setup(s => s.CreateCredentialsAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>()))
            .Callback<OAuthCredentials, CancellationToken>((c, _) => saved = c)
            .ReturnsAsync((OAuthCredentials c, CancellationToken _) => c);

        await _controller.Callback(routeSegment);

        saved.ShouldNotBeNull();
        saved.Provider.ShouldBe(routeSegment);
    }

    [Fact]
    public async Task Callback_DoesNotPersistCredentials_WhenAuthenticationFails()
    {
        _authenticationService
            .Setup(s => s.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
            .ReturnsAsync(AuthenticateResult.Fail("simulated failure"));

        await _controller.Callback("github");

        _credentialsService.Verify(
            s => s.CreateCredentialsAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Callback_RendersPopupPage_WhenNoReturnUrl()
    {
        SetUpSuccessfulAuthentication("Slack");
        var id = Guid.NewGuid();
        SetUpCreateReturningId(id);

        var result = (await _controller.Callback("slack")).ShouldBeOfType<ContentResult>();

        result.Content.ShouldContain("oauth-complete");
        result.Content.ShouldNotContain(id.ToString());

        var token = Regex.Match(result.Content!, "\"credentialToken\":\"([^\"]+)\"").Groups[1].Value;
        var handoff = Protector().Unprotect(token);
        handoff.Status.ShouldBe(OAuthCredentialsHandoffStatus.Valid);
        handoff.CredentialId.ShouldBe(id);
        handoff.Provider.ShouldBe("Slack");
    }

    [Fact]
    public async Task Callback_RedirectsToReturnUrl_WithCredentialTokenInFragment_WhenReturnUrlRoundTripped()
    {
        const string returnUrl = "/umbraco/section/automate/workspace/connection/edit/abc";
        SetUpSuccessfulAuthentication("Slack", returnUrl);
        var id = Guid.NewGuid();
        SetUpCreateReturningId(id);

        var result = (await _controller.Callback("slack")).ShouldBeOfType<RedirectResult>();

        var prefix = $"{returnUrl}#automate-oauth=1&provider=Slack&nonce=nonce-1&credentialToken=";
        result.Url.ShouldStartWith(prefix);
        result.Url.ShouldNotContain(id.ToString());

        var handoff = Protector().Unprotect(Uri.UnescapeDataString(result.Url[prefix.Length..]));
        handoff.Status.ShouldBe(OAuthCredentialsHandoffStatus.Valid);
        handoff.CredentialId.ShouldBe(id);
    }

    [Fact]
    public async Task Callback_IgnoresNonLocalReturnUrl_AndFallsBackToPopupPage()
    {
        // Defence in depth: even if a non-local URL somehow reached the properties, never redirect to it.
        SetUpSuccessfulAuthentication("Slack", "//evil.example/");
        SetUpCreateReturningId(Guid.NewGuid());

        var result = (await _controller.Callback("slack")).ShouldBeOfType<ContentResult>();

        result.Content.ShouldNotContain("evil.example");
    }

    [Fact]
    public async Task Callback_RedirectsBackWithError_WhenNoAccessTokenAndReturnUrlRoundTripped()
    {
        const string returnUrl = "/umbraco/section/automate";
        SetUpSuccessfulAuthentication("Slack", returnUrl, accessToken: null);

        var result = (await _controller.Callback("slack")).ShouldBeOfType<RedirectResult>();

        result.Url.ShouldBe($"{returnUrl}#automate-oauth=1&provider=Slack&nonce=nonce-1&error=No%20access%20token%20received.");
        _credentialsService.Verify(
            s => s.CreateCredentialsAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Callback_RedirectsBackWithError_WhenAuthenticationFailsButPropertiesCarryReturnUrl()
    {
        var properties = new AuthenticationProperties();
        properties.Items[OAuthReturnUrl.UrlPropertyKey] = "/umbraco";
        properties.Items[OAuthReturnUrl.NoncePropertyKey] = "nonce-1";
        _authenticationService
            .Setup(s => s.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
            .ReturnsAsync(AuthenticateResult.Fail("simulated failure", properties));

        var result = (await _controller.Callback("slack")).ShouldBeOfType<RedirectResult>();

        result.Url.ShouldBe("/umbraco#automate-oauth=1&provider=slack&nonce=nonce-1&error=Authentication%20failed.");
    }

    [Fact]
    public async Task ObsoleteConstructor_ResolvesNewDependenciesFromStaticServiceProvider()
    {
        // The single-argument constructor is kept for binary compatibility (removed in Umbraco 20).
        // It must resolve the dependencies added since from StaticServiceProvider and produce a
        // controller whose tokens the injected protector can read.
        var staticServices = new ServiceCollection()
            .AddSingleton<IDataProtectionProvider>(_dataProtectionProvider)
            .AddSingleton(TimeProvider.System)
            .BuildServiceProvider();

        var previous = StaticServiceProvider.Instance;
        StaticServiceProvider.Instance = staticServices;
        try
        {
#pragma warning disable CS0618 // Type or member is obsolete
            var controller = new OAuthCallbackController(_credentialsService.Object);
#pragma warning restore CS0618 // Type or member is obsolete
            controller.ControllerContext = _controller.ControllerContext;

            SetUpSuccessfulAuthentication("Slack");
            var id = Guid.NewGuid();
            SetUpCreateReturningId(id);

            var result = (await controller.Callback("slack")).ShouldBeOfType<ContentResult>();

            var token = Regex.Match(result.Content!, "\"credentialToken\":\"([^\"]+)\"").Groups[1].Value;
            var handoff = Protector().Unprotect(token);
            handoff.Status.ShouldBe(OAuthCredentialsHandoffStatus.Valid);
            handoff.CredentialId.ShouldBe(id);
        }
        finally
        {
            StaticServiceProvider.Instance = previous;
        }
    }

    private OAuthCredentialsHandoffProtector Protector() => new(_dataProtectionProvider, TimeProvider.System);

    private void SetUpCreateReturningId(Guid id) =>
        _credentialsService
            .Setup(s => s.CreateCredentialsAsync(It.IsAny<OAuthCredentials>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OAuthCredentials c, CancellationToken _) =>
            {
                c.Id = id;
                return c;
            });

    private void SetUpSuccessfulAuthentication(string? providerName, string? returnUrl = null, string? accessToken = "test-access-token")
    {
        var properties = new AuthenticationProperties();
        if (returnUrl is not null)
        {
            properties.Items[OAuthReturnUrl.UrlPropertyKey] = returnUrl;
            properties.Items[OAuthReturnUrl.NoncePropertyKey] = "nonce-1";
        }

        var tokens = new List<AuthenticationToken>
        {
            new() { Name = Tokens.RefreshToken, Value = "test-refresh-token" },
        };
        if (accessToken is not null)
        {
            tokens.Add(new AuthenticationToken { Name = Tokens.BackchannelAccessToken, Value = accessToken });
        }

        properties.StoreTokens(tokens);

        if (providerName is not null)
        {
            properties.SetString(Properties.ProviderName, providerName);
        }

        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity()),
            properties,
            OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);

        _authenticationService
            .Setup(s => s.AuthenticateAsync(It.IsAny<HttpContext>(), It.IsAny<string>()))
            .ReturnsAsync(AuthenticateResult.Success(ticket));
    }
}
