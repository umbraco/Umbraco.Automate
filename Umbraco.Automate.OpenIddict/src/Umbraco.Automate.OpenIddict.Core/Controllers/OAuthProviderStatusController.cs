using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.OpenIddict.Providers;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;

namespace Umbraco.Automate.OpenIddict.Controllers;

/// <summary>
/// Reports whether an OAuth provider has application credentials (client ID/secret) configured,
/// so the backoffice UI can warn before the user attempts to authenticate.
/// Requires backoffice authentication — unlike the challenge/callback endpoints, this isn't part
/// of the external OAuth redirect flow.
/// </summary>
[ApiController]
[Route("umbraco/automate/oauth")]
[MapToApi(Constants.OAuthApi.ApiName)]
[ApiExplorerSettings(GroupName = "OAuth")]
[Authorize(Policy = AuthorizationPolicies.BackOfficeAccess)]
public sealed class OAuthProviderStatusController : ControllerBase
{
    private readonly IOAuthProviderConfigurationSource _configurationSource;

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthProviderStatusController"/> class.
    /// </summary>
    public OAuthProviderStatusController(IOAuthProviderConfigurationSource configurationSource)
    {
        _configurationSource = configurationSource;
    }

    /// <summary>
    /// Gets whether the specified OAuth provider has a client ID and secret configured.
    /// </summary>
    /// <param name="provider">The OpenIddict provider name (e.g. "Slack", "Google").</param>
    [HttpGet("status/{provider}")]
    public ActionResult<OAuthProviderStatusResponseModel> Status(string provider)
    {
        var configuration = _configurationSource.GetConfiguration(provider);
        var isConfigured = !string.IsNullOrWhiteSpace(configuration?.ClientId)
            && !string.IsNullOrWhiteSpace(configuration?.ClientSecret);

        return Ok(new OAuthProviderStatusResponseModel(isConfigured));
    }
}

/// <summary>
/// Response for <see cref="OAuthProviderStatusController.Status"/>.
/// </summary>
/// <param name="IsConfigured">
/// True if the provider has a non-empty client ID and secret configured.
/// </param>
public sealed record OAuthProviderStatusResponseModel(bool IsConfigured);
