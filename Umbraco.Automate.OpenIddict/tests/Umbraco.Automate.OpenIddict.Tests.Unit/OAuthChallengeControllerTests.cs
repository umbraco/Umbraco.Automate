using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using OpenIddict.Client.AspNetCore;
using Umbraco.Automate.OpenIddict.Controllers;
using Umbraco.Automate.OpenIddict.Providers;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

/// <summary>
/// Regression tests for #106: dispatching a challenge for a provider with no client ID/secret
/// configured used to let OpenIddict throw an unhandled InvalidOperationException
/// ("A client identifier must be specified...") inside the auth popup. The controller now checks
/// configuration first and returns a friendly postMessage-and-close HTML page instead, mirroring
/// the pattern <see cref="OAuthCallbackController"/> uses for callback failures.
/// </summary>
public class OAuthChallengeControllerTests
{
    private readonly Mock<IOAuthProviderConfigurationSource> _configurationSource = new();
    private readonly OAuthChallengeController _controller;

    public OAuthChallengeControllerTests()
    {
        _controller = new OAuthChallengeController(_configurationSource.Object);

        // Challenge() calls Url.Action(...) to build the callback RedirectUri — not wired up
        // automatically outside the MVC request pipeline, so a minimal stub is needed.
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.Action(It.IsAny<UrlActionContext>())).Returns("/umbraco/automate/oauth/callback/slack");
        _controller.Url = urlHelper.Object;
    }

    [Fact]
    public void Challenge_ReturnsFriendlyHtml_WhenClientIdMissing()
    {
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = null, ClientSecret = "secret" });

        var result = _controller.Challenge("Slack", returnUrl: null, nonce: null).ShouldBeOfType<ContentResult>();

        result.ContentType.ShouldBe("text/html");
        result.Content.ShouldContain("oauth-complete");
        result.Content.ShouldContain("Slack is not configured");
    }

    [Fact]
    public void Challenge_ReturnsFriendlyHtml_WhenClientSecretMissing()
    {
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = "client-id", ClientSecret = "" });

        var result = _controller.Challenge("Slack", returnUrl: null, nonce: null).ShouldBeOfType<ContentResult>();

        result.Content.ShouldContain("oauth-complete");
        result.Content.ShouldContain("Slack is not configured");
    }

    [Fact]
    public void Challenge_ReturnsFriendlyHtml_WhenProviderHasNoConfigurationAtAll()
    {
        _configurationSource.Setup(s => s.GetConfiguration("Slack")).Returns((OAuthProviderConfiguration?)null);

        var result = _controller.Challenge("Slack", returnUrl: null, nonce: null).ShouldBeOfType<ContentResult>();

        result.Content.ShouldContain("oauth-complete");
    }

    [Fact]
    public void Challenge_DoesNotPostMessageSuccess_WhenProviderIsUnconfigured()
    {
        // The friendly-failure page must always report success: false — otherwise the property
        // editor would treat the misconfiguration as a completed authentication.
        _configurationSource.Setup(s => s.GetConfiguration("Slack")).Returns((OAuthProviderConfiguration?)null);

        var result = _controller.Challenge("Slack", returnUrl: null, nonce: null).ShouldBeOfType<ContentResult>();

        result.Content.ShouldContain("\"success\":false");
    }

    [Fact]
    public void Challenge_EncodesProviderName_SoItCannotInjectMarkup()
    {
        // #107: the provider route value is attacker-controllable and reflected into the popup
        // page, so it must be encoded — raw markup must never reach the response body or the
        // <script> postMessage payload.
        const string malicious = "</script><img src=x onerror=alert(1)>";
        _configurationSource.Setup(s => s.GetConfiguration(malicious)).Returns((OAuthProviderConfiguration?)null);

        var result = _controller.Challenge(malicious, returnUrl: null, nonce: null).ShouldBeOfType<ContentResult>();

        result.Content.ShouldNotContain("<img");
        result.Content.ShouldContain("&lt;img");
    }

    [Fact]
    public void Challenge_DispatchesRealChallenge_WhenProviderIsConfigured()
    {
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = "client-id", ClientSecret = "client-secret" });

        var result = _controller.Challenge("Slack", returnUrl: null, nonce: null).ShouldBeOfType<ChallengeResult>();

        result.AuthenticationSchemes.ShouldContain(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        result.Properties.ShouldNotBeNull();
        result.Properties!.GetString(OpenIddictClientAspNetCoreConstants.Properties.ProviderName).ShouldBe("Slack");
        result.Properties.Items.ContainsKey(OAuthReturnUrl.UrlPropertyKey).ShouldBeFalse();
    }

    [Fact]
    public void ObsoleteChallengeOverload_DelegatesToPopupFlow()
    {
        // The single-argument overload is kept for binary compatibility (removed in Umbraco 20) and
        // must behave exactly like the full overload with no return URL: a plain popup challenge.
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = "client-id", ClientSecret = "client-secret" });

#pragma warning disable CS0618 // Type or member is obsolete
        var result = _controller.Challenge("Slack").ShouldBeOfType<ChallengeResult>();
#pragma warning restore CS0618 // Type or member is obsolete

        result.AuthenticationSchemes.ShouldContain(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        result.Properties!.GetString(OpenIddictClientAspNetCoreConstants.Properties.ProviderName).ShouldBe("Slack");
        result.Properties.RedirectUri.ShouldBe("/umbraco/automate/oauth/callback/slack");
        result.Properties.Items.ContainsKey(OAuthReturnUrl.UrlPropertyKey).ShouldBeFalse();
    }

    [Fact]
    public void ObsoleteChallengeOverload_IsNotAnMvcAction()
    {
        // Without [NonAction], MVC would see two "Challenge" actions on the same route and fail
        // every request with an AmbiguousMatchException.
        var overload = typeof(OAuthChallengeController).GetMethod(
            nameof(OAuthChallengeController.Challenge),
            BindingFlags.Public | BindingFlags.Instance,
            [typeof(string)])!;

        overload.ShouldNotBeNull();
        overload.GetCustomAttribute<NonActionAttribute>().ShouldNotBeNull();
        overload.GetCustomAttribute<ObsoleteAttribute>().ShouldNotBeNull();
    }

    [Fact]
    public void Challenge_StoresReturnUrlInProtectedProperties_WhenLocal()
    {
        // Same-tab fallback: the return URL must travel inside the OpenIddict state token (the
        // authentication properties), never as a plain query parameter on the provider round trip.
        const string returnUrl = "/umbraco/section/automate/workspace/connection/edit/abc";
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = "client-id", ClientSecret = "client-secret" });

        var result = _controller.Challenge("Slack", returnUrl, "nonce-1").ShouldBeOfType<ChallengeResult>();

        result.Properties!.Items[OAuthReturnUrl.UrlPropertyKey].ShouldBe(returnUrl);
        result.Properties.Items[OAuthReturnUrl.NoncePropertyKey].ShouldBe("nonce-1");
        result.Properties.RedirectUri.ShouldBe("/umbraco/automate/oauth/callback/slack");
    }

    [Theory]
    [InlineData("https://evil.example/")]
    [InlineData("//evil.example/")]
    [InlineData("/\\evil.example/")]
    [InlineData("/\t/evil.example/")]
    public void Challenge_RejectsNonLocalReturnUrl_WithoutDispatchingChallenge(string returnUrl)
    {
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = "client-id", ClientSecret = "client-secret" });

        var result = _controller.Challenge("Slack", returnUrl, "nonce-1").ShouldBeOfType<ContentResult>();

        result.Content.ShouldContain("\"success\":false");
        result.Content.ShouldNotContain("evil.example");
    }

    [Fact]
    public void Challenge_RedirectsBackWithError_WhenUnconfiguredAndReturnUrlGiven()
    {
        // Same-tab flow: there is no opener to postMessage to, so the unconfigured-provider error
        // goes back to the backoffice in the fragment rather than stranding the user on this page.
        _configurationSource.Setup(s => s.GetConfiguration("Slack")).Returns((OAuthProviderConfiguration?)null);

        var result = _controller.Challenge("Slack", "/umbraco/section/automate", "nonce-1").ShouldBeOfType<RedirectResult>();

        result.Url.ShouldStartWith("/umbraco/section/automate#automate-oauth=1&provider=Slack&nonce=nonce-1&error=");
        result.Url.ShouldContain("not%20configured");
    }

    [Fact]
    public void Challenge_RejectsReturnUrlWithoutNonce()
    {
        // The nonce is what lets the editor tell its own result from a crafted link, so the
        // same-tab flow must never run without one.
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = "client-id", ClientSecret = "client-secret" });

        var result = _controller.Challenge("Slack", "/umbraco").ShouldBeOfType<ContentResult>();

        result.Content.ShouldContain("\"success\":false");
    }
}
