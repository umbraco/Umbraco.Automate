using Umbraco.Automate.Core.Actions;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Provides extension methods for configuring Umbraco Automate services on the Umbraco builder.
/// </summary>
public static partial class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds Umbraco Automate services to the Umbraco builder.
    /// </summary>
    public static IUmbracoBuilder AddUmbracoAutomate(this IUmbracoBuilder builder)
    {
        // Prevent multiple registrations
        if (builder.Services.Any(x => x.ServiceType == typeof(ActionCollection)))
            return builder;

        builder.AddUmbracoAutomateCore();
        builder.AddUmbracoAutomatePersistence();
        builder.AddUmbracoAutomateWeb();

        return builder;
    }
}
