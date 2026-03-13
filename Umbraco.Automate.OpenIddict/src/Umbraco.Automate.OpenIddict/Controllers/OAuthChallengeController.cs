using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Client.AspNetCore;
using Umbraco.Cms.Web.Common.Authorization;

namespace Umbraco.Automate.OpenIddict.Controllers;

/// <summary>
/// Initiates an OAuth challenge that redirects the user to the external provider's authorize page.
/// Requires backoffice authentication — only logged-in users can start an OAuth flow.
/// </summary>
[ApiController]
[Route("automate/oauth")]
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
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
