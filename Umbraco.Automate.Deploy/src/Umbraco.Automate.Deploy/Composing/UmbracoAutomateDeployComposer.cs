using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Deploy.Configuration;
using Umbraco.Automate.Deploy.NotificationHandlers;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Automate.Deploy.Composing;

/// <summary>
/// Composer for Umbraco Automate Deploy integration, responsible for registering services,
/// UDI types, and notification handlers to manage Automate artifacts during deployment.
/// </summary>
public class UmbracoAutomateDeployComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
    {
        // Configuration
        builder.Services.AddOptions<UmbracoAutomateDeploySettings>()
            .Bind(builder.Config.GetSection("Umbraco:Automate:Deploy"));

        builder.Services.AddSingleton<UmbracoAutomateDeploySettingsAccessor>();

        // Register component for UDI and disk entity type registration
        builder.Components()
            .Append<UmbracoAutomateDeployComponent>();

        // Register notification handlers for automatic artifact management — Saved
        builder.AddNotificationAsyncHandler<ConnectionSavedNotification,
            ConnectionSavedDeployRefresherNotificationAsyncHandler>();
        builder.AddNotificationAsyncHandler<WorkspaceSavedNotification,
            WorkspaceSavedDeployRefresherNotificationAsyncHandler>();
        builder.AddNotificationAsyncHandler<AutomationSavedNotification,
            AutomationSavedDeployRefresherNotificationAsyncHandler>();

        // Register notification handlers for automatic artifact management — Deleted
        builder.AddNotificationAsyncHandler<ConnectionDeletedNotification,
            ConnectionDeletedDeployRefresherNotificationAsyncHandler>();
        builder.AddNotificationAsyncHandler<WorkspaceDeletedNotification,
            WorkspaceDeletedDeployRefresherNotificationAsyncHandler>();
        builder.AddNotificationAsyncHandler<AutomationDeletedNotification,
            AutomationDeletedDeployRefresherNotificationAsyncHandler>();
    }
}
