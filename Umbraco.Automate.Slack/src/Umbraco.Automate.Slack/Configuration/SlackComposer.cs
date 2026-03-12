using Microsoft.Extensions.DependencyInjection;

using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Automate.Slack.Configuration;

/// <summary>
/// Registers the Slack OAuth provider with OpenIddict Client WebIntegration.
/// Credentials and redirect URI are applied automatically by the OpenIddict package.
/// </summary>
public sealed class SlackComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddOpenIddict()
            .AddClient(options => options.UseWebProviders().AddSlack(_ => { }));
    }
}
