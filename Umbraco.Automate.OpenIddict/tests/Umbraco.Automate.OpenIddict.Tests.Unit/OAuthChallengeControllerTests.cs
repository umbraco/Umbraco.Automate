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

        var result = _controller.Challenge("Slack").ShouldBeOfType<ContentResult>();

        result.ContentType.ShouldBe("text/html");
        result.Content.ShouldContain("oauth-complete");
        result.Content.ShouldContain("Slack is not configured");
    }

    [Fact]
    public void Challenge_ReturnsFriendlyHtml_WhenClientSecretMissing()
    {
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = "client-id", ClientSecret = "" });

        var result = _controller.Challenge("Slack").ShouldBeOfType<ContentResult>();

        result.Content.ShouldContain("oauth-complete");
        result.Content.ShouldContain("Slack is not configured");
    }

    [Fact]
    public void Challenge_ReturnsFriendlyHtml_WhenProviderHasNoConfigurationAtAll()
    {
        _configurationSource.Setup(s => s.GetConfiguration("Slack")).Returns((OAuthProviderConfiguration?)null);

        var result = _controller.Challenge("Slack").ShouldBeOfType<ContentResult>();

        result.Content.ShouldContain("oauth-complete");
    }

    [Fact]
    public void Challenge_DoesNotPostMessageSuccess_WhenProviderIsUnconfigured()
    {
        // The friendly-failure page must always report success: false — otherwise the property
        // editor would treat the misconfiguration as a completed authentication.
        _configurationSource.Setup(s => s.GetConfiguration("Slack")).Returns((OAuthProviderConfiguration?)null);

        var result = _controller.Challenge("Slack").ShouldBeOfType<ContentResult>();

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

        var result = _controller.Challenge(malicious).ShouldBeOfType<ContentResult>();

        result.Content.ShouldNotContain("<img");
        result.Content.ShouldContain("&lt;img");
    }

    [Fact]
    public void Challenge_DispatchesRealChallenge_WhenProviderIsConfigured()
    {
        _configurationSource.Setup(s => s.GetConfiguration("Slack"))
            .Returns(new OAuthProviderConfiguration { ClientId = "client-id", ClientSecret = "client-secret" });

        var result = _controller.Challenge("Slack").ShouldBeOfType<ChallengeResult>();

        result.AuthenticationSchemes.ShouldContain(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
        result.Properties.ShouldNotBeNull();
        result.Properties!.GetString(OpenIddictClientAspNetCoreConstants.Properties.ProviderName).ShouldBe("Slack");
    }
}
