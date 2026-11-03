using Microsoft.AspNetCore.Authorization;
using OpenIddict.Validation.AspNetCore;
using CmsConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Web.Authorization;

internal static class AutomateAuthorizationPolicyConfiguration
{
    internal static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(AutomateAuthorizationPolicies.SectionAccessAutomate, policy =>
        {
            AddBackOfficeAuthenticationSchemes(policy);
            policy.Requirements.Add(new AutomateSectionRequirement());
        });

        options.AddPolicy(AutomateAuthorizationPolicies.WorkspaceAccess, policy =>
        {
            AddBackOfficeAuthenticationSchemes(policy);
            policy.Requirements.Add(new WorkspaceAccessRequirement());
        });
    }

    // Mirrors the schemes the CMS adds to its own Management API policies. From CMS 19 the backoffice
    // authenticates Management API calls with the httpOnly cookie, and the OpenIddict bearer token is
    // still accepted for API clients. The CMS helper is internal, so the pair is repeated here. It
    // is the same set the CMS accepts, nothing wider, and the CMS relies on the cookie's SameSite
    // attribute rather than antiforgery tokens for state-changing calls.
    private static void AddBackOfficeAuthenticationSchemes(AuthorizationPolicyBuilder policy)
        => policy.AddAuthenticationSchemes(
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme,
            CmsConstants.Security.BackOfficeAuthenticationType);
}
