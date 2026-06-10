using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Client.AspNetCore;
using Umbraco.Cms.Api.Common.Attributes;

namespace Umbraco.Automate.OpenIddict.Controllers;

/// <summary>
/// Initiates an OAuth challenge that redirects the user to the external provider's authorize page.
/// No authorization attribute — the endpoint only redirects to the external provider.
/// Security is enforced by the state parameter validated on the callback.
/// </summary>
[ApiController]
[Route("umbraco/automate/oauth")]
[MapToApi(Constants.OAuthApi.ApiName)]
[ApiExplorerSettings(GroupName = "OAuth")]
public sealed class OAuthChallengeController : ControllerBase
{
    /// <summary>
    /// Initiates an OAuth authorization code flow for the specified provider.
    /// Opens in a popup — the callback will close the popup when complete.
    /// </summary>
    /// <param name="provider">The OpenIddict provider name (e.g. "Slack", "GitHub").</param>
    [HttpGet("challenge/{provider}")]
    public IActionResult Challenge(string provider)
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(
                nameof(OAuthCallbackController.Callback),
                "OAuthCallback",
                new { provider }),
        };

        return Challenge(properties, OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);
    }
}
