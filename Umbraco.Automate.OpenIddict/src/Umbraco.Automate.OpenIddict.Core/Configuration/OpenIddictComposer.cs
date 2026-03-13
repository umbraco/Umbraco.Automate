using Umbraco.Automate.OpenIddict.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Automate.OpenIddict.Configuration;

/// <summary>
/// Registers Umbraco Automate OpenIddict services — OAuth credential storage and
/// OpenIddict Client with WebIntegration support.
/// </summary>
public sealed class OpenIddictComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddAutomateOpenIddict();
    }
}
