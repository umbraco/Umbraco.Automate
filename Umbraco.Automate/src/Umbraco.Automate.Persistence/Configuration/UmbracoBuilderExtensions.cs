using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Triggers.Scheduling;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Workspaces;
using Umbraco.Automate.Persistence.Connections;
using Umbraco.Automate.Persistence.Notifications;
using Umbraco.Automate.Persistence.Triggers;
using Umbraco.Automate.Persistence;
using Umbraco.Automate.Persistence.Outbox;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Persistence.Versioning;
using Umbraco.Automate.Persistence.Workflows;
using Umbraco.Automate.Core.Persistence;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Extension methods for configuring Umbraco Automate persistence.
/// </summary>
public static partial class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds EF Core persistence for Umbraco Automate.
    /// </summary>
    public static IUmbracoBuilder AddUmbracoAutomatePersistence(this IUmbracoBuilder builder)
    {
        // Resolve the connection string lazily inside the factory (run time), not here at
        // composition time: hosts like Umbraco Cloud / Deploy synthesise the DSN through the
        // ConnectionStrings options pipeline, which has not run yet during AddComposers().
        builder.Services.AddUmbracoDbContext<UmbracoAutomateDbContext>((serviceProvider, options, _, _) =>
        {
            var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(
                serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                serviceProvider.GetRequiredService<IConfiguration>());
            UmbracoAutomateDbContext.ConfigureProvider(options, connectionString, providerName);
        });

        builder.Services.AddSingleton<AutomationFactory>();
        builder.Services.AddSingleton<ConnectionFactory>();
        builder.Services.AddSingleton<IAutomationRepository, EFCoreAutomationRepository>();
        builder.Services.AddSingleton<WorkspaceGroupFactory>();
        builder.Services.AddSingleton<IWorkspaceGroupRepository, EFCoreWorkspaceGroupRepository>();
        builder.Services.AddSingleton<IWorkspaceRepository, EFCoreWorkspaceRepository>();
        builder.Services.AddSingleton<IConnectionRepository, EFCoreConnectionRepository>();
        builder.Services.AddSingleton<IAutomationRunRepository, EFCoreAutomationRunRepository>();
        builder.Services.AddSingleton<IAutomationHealthRepository, EFCoreAutomationHealthRepository>();
        // WorkflowCore's EventConsumer, WorkflowConsumer etc. inject the sub-interfaces directly,
        // not IPersistenceProvider. We must register all four so our EF provider is used everywhere.
        builder.Services.AddSingleton<EFCoreWorkflowPersistenceProvider>();
        builder.Services.AddSingleton<IPersistenceProvider>(sp => sp.GetRequiredService<EFCoreWorkflowPersistenceProvider>());
        builder.Services.AddSingleton<IWorkflowRepository>(sp => sp.GetRequiredService<EFCoreWorkflowPersistenceProvider>());
        builder.Services.AddSingleton<ISubscriptionRepository>(sp => sp.GetRequiredService<EFCoreWorkflowPersistenceProvider>());
        builder.Services.AddSingleton<IEventRepository>(sp => sp.GetRequiredService<EFCoreWorkflowPersistenceProvider>());
        builder.Services.AddSingleton<IOutboxStore, EFCoreOutboxStore>();
        builder.Services.AddSingleton<IEntityVersionRepository, EFCoreEntityVersionRepository>();
        builder.Services.AddSingleton<IScheduledTriggerStateStore, ScheduledTriggerStateStore>();

        // Run pending EF Core migrations on startup.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunAutomateMigrationNotificationHandler>();

        // Recover runs stuck in Running/Pending from the previous process.
        // Registered after migrations so the schema is up-to-date.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, StuckRunRecoveryNotificationHandler>();

        return builder;
    }
}
