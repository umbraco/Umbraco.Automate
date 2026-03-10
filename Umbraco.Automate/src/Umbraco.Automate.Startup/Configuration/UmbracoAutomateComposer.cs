using Umbraco.Automate.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Automate.Startup.Configuration;

/// <summary>
/// Composer that registers Umbraco Automate services with the DI container.
/// </summary>
public class UmbracoAutomateComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddUmbracoAutomate();
    }
}
