using System.Collections.Immutable;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Client.WebIntegration;
using static OpenIddict.Client.OpenIddictClientEvents;
using static OpenIddict.Client.WebIntegration.OpenIddictClientWebIntegrationConstants;

namespace Umbraco.Automate.Slack.Configuration;

/// <summary>
/// Custom OpenIddict event handlers that redirect the Slack OAuth flow from the
/// OIDC "Sign in with Slack" endpoints to the Slack OAuth V2 endpoints.
/// This enables bot token scopes (e.g. <c>chat:write</c>) and user token scopes
/// (e.g. <c>search:read</c>) in a single authorization flow.
/// </summary>
/// <remarks>
/// <para>
/// The built-in OpenIddict <c>AddSlack()</c> WebIntegration provider uses Slack's OIDC
/// discovery (<c>https://slack.com/.well-known/openid-configuration</c>), which resolves to:
/// <list type="bullet">
///   <item>Authorize: <c>https://slack.com/openid/connect/authorize</c></item>
///   <item>Token: <c>https://slack.com/api/openid.connect.token</c></item>
/// </list>
/// These only support user identity scopes (<c>openid</c>, <c>profile</c>, <c>email</c>).
/// </para>
/// <para>
/// Slack's V2 OAuth uses different endpoints:
/// <list type="bullet">
///   <item>Authorize: <c>https://slack.com/oauth/v2/authorize</c></item>
///   <item>Token: <c>https://slack.com/api/oauth.v2.access</c></item>
/// </list>
/// Bot scopes go in the <c>scope</c> parameter, user scopes in <c>user_scope</c>.
/// The token response returns a bot token at <c>access_token</c> and optionally
/// a user token at <c>authed_user.access_token</c>.
/// </para>
/// </remarks>
internal static class SlackOAuthHandlers
{
    public static ImmutableArray<OpenIddictClientHandlerDescriptor> Descriptors { get; } =
    [
        OverrideAuthorizationEndpoint.Descriptor,
        AttachUserScopeParameter.Descriptor,
        OverrideTokenEndpoint.Descriptor,
        DisableBackchannelIdentityToken.Descriptor,
        DisableUserInfoRetrieval.Descriptor,
        ExtractUserAccessToken.Descriptor,
    ];

    private static bool IsSlack(OpenIddictClientRegistration registration)
        => registration.ProviderType == ProviderTypes.Slack;

    /// <summary>
    /// Overrides the authorization endpoint from the OIDC discovery value to the V2 endpoint.
    /// </summary>
    internal sealed class OverrideAuthorizationEndpoint : IOpenIddictClientHandler<ProcessChallengeContext>
    {
        public static OpenIddictClientHandlerDescriptor Descriptor { get; }
            = OpenIddictClientHandlerDescriptor.CreateBuilder<ProcessChallengeContext>()
                .UseSingletonHandler<OverrideAuthorizationEndpoint>()
                // Run after the built-in OverrideAuthorizationEndpoint (which doesn't touch Slack)
                // but before AttachChallengeParameters builds the final request.
                .SetOrder(OpenIddictClientWebIntegrationHandlers
                    .OverrideAuthorizationEndpoint.Descriptor.Order + 1)
                .Build();

        public ValueTask HandleAsync(ProcessChallengeContext context)
        {
            if (IsSlack(context.Registration))
            {
                context.AuthorizationEndpoint = new Uri("https://slack.com/oauth/v2/authorize", UriKind.Absolute);
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Moves user-scoped permissions from the standard <c>scope</c> parameter to
    /// Slack's non-standard <c>user_scope</c> parameter. Bot scopes remain in <c>scope</c>.
    /// </summary>
    /// <remarks>
    /// Slack V2 uses <c>scope</c> for bot scopes and <c>user_scope</c> for user scopes.
    /// User scopes are configured via <c>Umbraco:Automate:Providers:Slack:UserScopes</c>.
    /// The standard <c>scope</c> parameter (built by OpenIddict from the registration's scopes)
    /// is left as-is — it becomes the bot scope list.
    /// </remarks>
    internal sealed class AttachUserScopeParameter : IOpenIddictClientHandler<ProcessChallengeContext>
    {
        private readonly ISlackScopeConfiguration _scopeConfiguration;

        public AttachUserScopeParameter(ISlackScopeConfiguration scopeConfiguration)
        {
            _scopeConfiguration = scopeConfiguration;
        }

        public static OpenIddictClientHandlerDescriptor Descriptor { get; }
            = OpenIddictClientHandlerDescriptor.CreateBuilder<ProcessChallengeContext>()
                .UseScopedHandler<AttachUserScopeParameter>()
                // Run after FormatNonStandardScopeParameter so the scope param is finalized,
                // then attach user_scope alongside it.
                .SetOrder(OpenIddictClientWebIntegrationHandlers
                    .FormatNonStandardScopeParameter.Descriptor.Order + 1)
                .Build();

        public ValueTask HandleAsync(ProcessChallengeContext context)
        {
            if (!IsSlack(context.Registration))
            {
                return ValueTask.CompletedTask;
            }

            // Strip OIDC-only scopes that the WebIntegration provider may have added.
            var botScopes = context.Scopes
                .Where(s => !string.Equals(s, "openid", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(s, "profile", StringComparison.OrdinalIgnoreCase)
                         && !string.Equals(s, "email", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Slack V2 uses comma-separated scopes.
            // When no bot scopes are configured, omit the parameter entirely
            // so Slack treats this as a user-only authorization.
            context.Request.Scope = botScopes.Count > 0
                ? string.Join(",", botScopes)
                : null;

            var userScopes = _scopeConfiguration.UserScopes;
            if (userScopes.Length > 0)
            {
                context.Request["user_scope"] = string.Join(",", userScopes);
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Overrides the token endpoint from the OIDC discovery value to the V2 endpoint.
    /// </summary>
    internal sealed class OverrideTokenEndpoint : IOpenIddictClientHandler<ProcessAuthenticationContext>
    {
        public static OpenIddictClientHandlerDescriptor Descriptor { get; }
            = OpenIddictClientHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
                .UseSingletonHandler<OverrideTokenEndpoint>()
                // Run after the built-in OverrideTokenEndpoint (which doesn't touch Slack).
                .SetOrder(OpenIddictClientWebIntegrationHandlers
                    .OverrideTokenEndpoint.Descriptor.Order + 1)
                .Build();

        public ValueTask HandleAsync(ProcessAuthenticationContext context)
        {
            if (IsSlack(context.Registration))
            {
                context.TokenEndpoint = new Uri("https://slack.com/api/oauth.v2.access", UriKind.Absolute);
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Adjusts backchannel token validation for Slack V2:
    /// <list type="bullet">
    ///   <item>Always disables identity token (V2 never returns <c>id_token</c>)</item>
    ///   <item>For user-only flows (no bot scopes), disables the access token requirement
    ///         since the useful token is under <c>authed_user</c>, not at the top level</item>
    /// </list>
    /// </summary>
    internal sealed class DisableBackchannelIdentityToken : IOpenIddictClientHandler<ProcessAuthenticationContext>
    {
        private readonly ISlackScopeConfiguration _scopeConfiguration;

        public DisableBackchannelIdentityToken(ISlackScopeConfiguration scopeConfiguration)
        {
            _scopeConfiguration = scopeConfiguration;
        }

        public static OpenIddictClientHandlerDescriptor Descriptor { get; }
            = OpenIddictClientHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
                .UseScopedHandler<DisableBackchannelIdentityToken>()
                // Run after the built-in OverrideValidatedBackchannelTokens.
                .SetOrder(OpenIddictClientWebIntegrationHandlers
                    .OverrideValidatedBackchannelTokens.Descriptor.Order + 1)
                .Build();

        public ValueTask HandleAsync(ProcessAuthenticationContext context)
        {
            if (!IsSlack(context.Registration))
            {
                return ValueTask.CompletedTask;
            }

            // V2 never returns an id_token.
            context.ExtractBackchannelIdentityToken = false;
            context.RequireBackchannelIdentityToken = false;
            context.ValidateBackchannelIdentityToken = false;

            // User-only flow: no bot scopes means no top-level access_token.
            // Relax the requirement so the pipeline doesn't reject the response.
            if (context.Registration.Scopes.All(s =>
                    string.Equals(s, "openid", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "profile", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(s, "email", StringComparison.OrdinalIgnoreCase))
                && _scopeConfiguration.UserScopes.Length > 0)
            {
                context.RequireBackchannelAccessToken = false;
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Disables the userinfo request for Slack V2. The OIDC userinfo endpoint
    /// (<c>/api/openid.connect.userInfo</c>) rejects bot tokens, and V2 doesn't
    /// use it — team/user info is in the token response instead.
    /// </summary>
    internal sealed class DisableUserInfoRetrieval : IOpenIddictClientHandler<ProcessAuthenticationContext>
    {
        public static OpenIddictClientHandlerDescriptor Descriptor { get; }
            = OpenIddictClientHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
                .UseSingletonHandler<DisableUserInfoRetrieval>()
                // Run after the built-in OverrideUserInfoRetrieval.
                .SetOrder(OpenIddictClientWebIntegrationHandlers
                    .OverrideUserInfoRetrieval.Descriptor.Order + 1)
                .Build();

        public ValueTask HandleAsync(ProcessAuthenticationContext context)
        {
            if (IsSlack(context.Registration))
            {
                context.SendUserInfoRequest = false;
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Extracts the user access token from the non-standard Slack V2 token response
    /// and stashes it in the authentication properties so the callback controller can store it.
    /// </summary>
    /// <remarks>
    /// The Slack V2 token response nests the user token under <c>authed_user.access_token</c>.
    /// OpenIddict only extracts the top-level <c>access_token</c> (the bot token).
    /// This handler reads the nested value and stores it as a custom authentication property.
    /// </remarks>
    internal sealed class ExtractUserAccessToken : IOpenIddictClientHandler<ProcessAuthenticationContext>
    {
        public static OpenIddictClientHandlerDescriptor Descriptor { get; }
            = OpenIddictClientHandlerDescriptor.CreateBuilder<ProcessAuthenticationContext>()
                .UseSingletonHandler<ExtractUserAccessToken>()
                // Run late — after all token processing is done.
                .SetOrder(OpenIddictClientWebIntegrationHandlers
                    .PopulateUserInfoTokenPrincipalFromTokenResponse.Descriptor.Order + 1)
                .Build();

        public ValueTask HandleAsync(ProcessAuthenticationContext context)
        {
            if (!IsSlack(context.Registration)
                || context.GrantType is not OpenIddictConstants.GrantTypes.AuthorizationCode
                || context.TokenResponse is null)
            {
                return ValueTask.CompletedTask;
            }

            // Slack V2 nests the user token under "authed_user".
            var userAccessToken = (string?)context.TokenResponse["authed_user"]?["access_token"];
            var hasBotToken = !string.IsNullOrEmpty(context.BackchannelAccessToken);

            if (!string.IsNullOrEmpty(userAccessToken))
            {
                if (hasBotToken)
                {
                    // Bot + user flow: store the user token as the secondary token.
                    context.Properties[OpenIddict.Constants.OAuthProperties.UserAccessToken] = userAccessToken;
                }
                else
                {
                    // User-only flow: no bot token, so promote the user token to the
                    // primary backchannel access token so OpenIddict propagates it normally.
                    context.BackchannelAccessToken = userAccessToken;
                }
            }

            // Extract bot scopes from the top-level response (comma-separated → space-separated).
            var botScope = (string?)context.TokenResponse["scope"];
            if (!string.IsNullOrEmpty(botScope))
            {
                context.Properties[OpenIddict.Constants.OAuthProperties.Scopes] =
                    botScope.Replace(',', ' ');
            }

            // For user-only flows, extract user scopes instead.
            if (!hasBotToken)
            {
                var userScope = (string?)context.TokenResponse["authed_user"]?["scope"];
                if (!string.IsNullOrEmpty(userScope))
                {
                    context.Properties[OpenIddict.Constants.OAuthProperties.Scopes] =
                        userScope.Replace(',', ' ');
                }
            }

            // Extract team info for the account label.
            var teamName = (string?)context.TokenResponse["team"]?["name"];
            if (!string.IsNullOrEmpty(teamName))
            {
                context.Properties[OpenIddict.Constants.OAuthProperties.AccountLabel] = teamName;
            }

            return ValueTask.CompletedTask;
        }
    }

}
