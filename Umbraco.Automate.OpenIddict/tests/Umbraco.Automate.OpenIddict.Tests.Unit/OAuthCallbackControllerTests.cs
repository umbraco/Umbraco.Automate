using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client.AspNetCore;
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
    private readonly OAuthCallbackController _controller;

    public OAuthCallbackControllerTests()
    {
        _controller = new OAuthCallbackController(_credentialsService.Object);

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

    private void SetUpSuccessfulAuthentication(string? providerName)
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens(
            [
                new AuthenticationToken { Name = Tokens.BackchannelAccessToken, Value = "test-access-token" },
                new AuthenticationToken { Name = Tokens.RefreshToken, Value = "test-refresh-token" },
            ]);

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
