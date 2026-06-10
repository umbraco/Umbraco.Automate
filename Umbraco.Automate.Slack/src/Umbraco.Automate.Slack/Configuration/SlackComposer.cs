using Microsoft.Extensions.DependencyInjection;

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Automate.Slack.Configuration;

/// <summary>
/// Registers the Slack OAuth provider with OpenIddict Client WebIntegration and
/// adds custom event handlers that redirect the flow to Slack's OAuth V2 endpoints.
/// This enables bot scopes and user scopes instead of the default OIDC "Sign in with Slack" flow.
/// </summary>
public sealed class SlackComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddSingleton<ISlackScopeConfiguration, ConfigurationSlackScopeConfiguration>();

        builder.Services.AddOpenIddict()
            .AddClient(options =>
            {
                options.UseWebProviders().AddSlack(_ => { });

                // Register the Slack V2 event handlers that override the OIDC endpoints.
                foreach (var descriptor in SlackOAuthHandlers.Descriptors)
                {
                    options.AddEventHandler(descriptor);
                }
            });
    }
}
