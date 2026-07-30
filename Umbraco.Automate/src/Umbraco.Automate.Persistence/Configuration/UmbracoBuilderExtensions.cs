using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
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
        //
        // shareUmbracoConnection: false — Automate defaults to its own database, and even when it is
        // pointed at umbracoDbDSN nothing here resolves an IEfCoreScope<UmbracoAutomateDbContext>.
        // Sharing the ambient connection is handled by AmbientAutomateDbContextFactory below, which
        // decides per call rather than once at composition time.
        builder.Services.AddUmbracoDbContext<UmbracoAutomateDbContext>(
            (IServiceProvider serviceProvider, DbContextOptionsBuilder options, string? _, string? _) =>
            {
                var (connectionString, providerName) = DatabaseConnectionInfo.Resolve(
                    serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                    serviceProvider.GetRequiredService<IConfiguration>());
                UmbracoAutomateDbContext.ConfigureProvider(options, connectionString, providerName);

                // Defer writes on this (DI-resolved) context until Automate's own startup migrations
                // have completed. The migration handler's own standalone context is configured
                // separately, via the same ConfigureProvider call, and is deliberately not gated here.
                options.AddInterceptors(new AutomateReadinessInterceptor(
                    serviceProvider.GetRequiredService<AutomateReadinessSignal>()));
            },
            shareUmbracoConnection: false);

        // Decorate the pooled factory so that when Automate is pointed at the Umbraco CMS database
        // (Umbraco:Automate:UseNamedConnectionString: umbracoDbDSN — the required setting on Umbraco
        // Cloud, where the CMS connection string is not user-editable) its reads and writes join the
        // ambient Umbraco transaction instead of opening a second connection to the same database.
        // On SQLite a second connection cannot get the write lock the ambient scope is holding, which
        // deadlocks any caller that keeps a transaction open across an Automate write — Umbraco
        // Deploy's restore being the reported case. See AmbientDbContextFactory.
        builder.Services.EnlistDbContextFactoryInAmbientScope(
            (serviceProvider, connection) =>
            {
                var (_, providerName) = DatabaseConnectionInfo.Resolve(
                    serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                    serviceProvider.GetRequiredService<IConfiguration>());

                var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
                UmbracoAutomateDbContext.ConfigureProvider(options, connection, providerName);

                // Mirrors the interceptor on the pooled factory, so a write is gated on Automate's
                // startup migrations whichever path it takes.
                options.AddInterceptors(new AutomateReadinessInterceptor(
                    serviceProvider.GetRequiredService<AutomateReadinessSignal>()));

                return new UmbracoAutomateDbContext(options.Options);
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
        builder.Services.AddSingleton<IWorkflowLockStore, EFCoreWorkflowLockStore>();
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
