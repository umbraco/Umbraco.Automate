using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        builder.Services.AddUmbracoAutomateDbContext();

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
        builder.Services.AddSingleton<IWorkflowPurger, EFCoreWorkflowPurger>();
        builder.Services.AddSingleton<IWorkflowLockStore, EFCoreWorkflowLockStore>();
        builder.Services.AddSingleton<IWorkflowNodeHeartbeatStore, EFCoreWorkflowNodeHeartbeatStore>();
        builder.Services.AddSingleton<IEntityVersionRepository, EFCoreEntityVersionRepository>();
        builder.Services.AddSingleton<IScheduledTriggerStateStore, ScheduledTriggerStateStore>();

        builder.Services.AddSingleton<IAutomateSchemaInitializer, AutomateSchemaInitializer>();

        // One-off repair: encrypt sensitive automation settings that earlier versions stored in plaintext.
        builder.Services.AddHostedService<SensitiveSettingsReprotectionJob>();

        // Run pending EF Core migrations during component initialization, which both boot paths do
        // immediately before publishing UmbracoApplicationStartingNotification. That puts the schema
        // in place before any Starting handler can query it — notably Umbraco Deploy's boot-time
        // restore. See AutomateSchemaComponent for why a Started handler was too late.
        builder.Components().Append<AutomateSchemaComponent>();

        // Safety net for any boot path that does not initialize components first. No-op once the
        // component above has run.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunAutomateMigrationNotificationHandler>();

        // Recovers runs stuck in Running/Pending from the previous process. Invoked by
        // WorkflowHostLifecycle once migrations are done and before the engine starts.
        builder.Services.AddSingleton<IStuckRunRecovery, EFCoreStuckRunRecovery>();

        return builder;
    }

    /// <summary>
    /// Registers the pooled <see cref="UmbracoAutomateDbContext"/> factory and enlists it in the ambient
    /// Umbraco scope. Split out of <see cref="AddUmbracoAutomatePersistence"/> so the composition tests
    /// exercise this exact registration instead of a hand-kept copy of it.
    /// </summary>
    internal static IServiceCollection AddUmbracoAutomateDbContext(this IServiceCollection services)
    {
        // See AddAutomatePooledDbContext for how the connection string is resolved and why sharing the
        // ambient connection is decided per call. The enlisted context below is what lets Automate
        // join the ambient Umbraco transaction when it is pointed at umbracoDbDSN; on SQLite a second
        // connection would deadlock on the held write lock (Umbraco Deploy restore).
        services.AddAutomatePooledDbContext<UmbracoAutomateDbContext>(
            AutomatePooledDbContextOptions.Configure,
            (serviceProvider, connection, providerName) =>
            {
                var options = new DbContextOptionsBuilder<UmbracoAutomateDbContext>();
                UmbracoAutomateDbContext.ConfigureProvider(options, connection, providerName);

                // Mirrors the interceptor on the pooled factory, so a write is gated on Automate's
                // startup migrations whichever path it takes — but with a timeout, because this path
                // waits while holding the caller's transaction (and on SQLite, its write lock).
                options.AddInterceptors(new AutomateReadinessInterceptor(
                    serviceProvider.GetRequiredService<AutomateReadinessSignal>(),
                    AutomateReadinessInterceptor.EnlistedWaitTimeout));

                return new UmbracoAutomateDbContext(options.Options);
            },
            placeholderWarning:
                "Umbraco Automate has no database connection string configured yet. Automate stays " +
                "inactive until one is configured. A connection string added by the installer is picked " +
                "up straight away; one added any other way needs the application to restart.",
            foundMessage: "Umbraco Automate found its database connection string and is now using it.",
            loggerCategory: "Umbraco.Automate.Extensions.UmbracoBuilderExtensions");

        return services;
    }
}

/// <summary>
/// Applies the provider setup for the pooled <see cref="UmbracoAutomateDbContext"/> factory.
/// </summary>
/// <remarks>
/// <para>
/// Not nested in <see cref="UmbracoBuilderExtensions"/>: that name is a
/// <see langword="partial"/> class repeated (as a distinct type) in both
/// <c>Umbraco.Automate.Core</c> and <c>Umbraco.Automate.Persistence</c>, which is fine for extension
/// methods invoked through instance syntax but makes a direct, unqualified reference to the type
/// itself ambiguous for any assembly that ends up referencing both — as the integration tests here
/// do. A distinctly-named type sidesteps that.
/// </para>
/// <para>
/// Resolving the connection string, the placeholder fallback for a not-yet-configured site and the
/// switch to the real connection string once it appears all live in
/// <c>AddAutomatePooledDbContext</c> and <c>ResolvingPooledDbContextFactory</c>
/// (see <see href="https://github.com/umbraco/Umbraco.Automate/issues/531"/>). This only configures the
/// provider for whichever connection string they hand it.
/// </para>
/// </remarks>
internal static class AutomatePooledDbContextOptions
{
    internal static void Configure(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder options,
        string connectionString,
        string providerName)
    {
        UmbracoAutomateDbContext.ConfigureProvider(options, connectionString, providerName);

        // Defer writes on this (DI-resolved) context until Automate's own startup migrations
        // have completed. The migration handler's own standalone context is configured
        // separately, via the same ConfigureProvider call, and is deliberately not gated here.
        options.AddInterceptors(new AutomateReadinessInterceptor(
            serviceProvider.GetRequiredService<AutomateReadinessSignal>()));
    }
}
