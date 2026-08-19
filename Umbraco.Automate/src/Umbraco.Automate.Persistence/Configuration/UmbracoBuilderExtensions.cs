using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
            AutomatePooledDbContextOptions.Configure,
            shareUmbracoConnection: false);

        // Decorate the pooled factory so that when Automate is pointed at the Umbraco CMS database
        // (Umbraco:Automate:UseNamedConnectionString: umbracoDbDSN — the required setting on Umbraco
        // Cloud, where the CMS connection string is not user-editable) its reads and writes join the
        // ambient Umbraco transaction instead of opening a second connection to the same database.
        // On SQLite a second connection cannot get the write lock the ambient scope is holding, which
        // deadlocks any caller that keeps a transaction open across an Automate write — Umbraco
        // Deploy's restore being the reported case. See AmbientDbContextFactory.
        builder.Services.EnlistDbContextFactoryInAmbientScope(
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

        builder.Services.AddSingleton<IAutomateSchemaInitializer, AutomateSchemaInitializer>();

        // Run pending EF Core migrations during component initialization, which both boot paths do
        // immediately before publishing UmbracoApplicationStartingNotification. That puts the schema
        // in place before any Starting handler can query it — notably Umbraco Deploy's boot-time
        // restore. See AutomateSchemaComponent for why a Started handler was too late.
        builder.Components().Append<AutomateSchemaComponent>();

        // Safety net for any boot path that does not initialize components first. No-op once the
        // component above has run.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, RunAutomateMigrationNotificationHandler>();

        // Recover runs stuck in Running/Pending from the previous process.
        // Registered after migrations so the schema is up-to-date.
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, StuckRunRecoveryNotificationHandler>();

        return builder;
    }
}

/// <summary>
/// Configures the pooled <see cref="UmbracoAutomateDbContext"/> factory's options.
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
/// Resolve the connection string lazily inside the factory (run time), not at composition time:
/// hosts like Umbraco Cloud / Deploy synthesise the DSN through the ConnectionStrings options
/// pipeline, which has not run yet during AddComposers(). But "lazily" only reaches as far as
/// <c>AddPooledDbContextFactory</c> allows — it builds this delegate's
/// <see cref="DbContextOptionsBuilder"/> the moment something first resolves
/// <c>IDbContextFactory&lt;UmbracoAutomateDbContext&gt;</c>, not per <c>CreateDbContext()</c> call.
/// Automate's own background services (the outbox dispatcher, the WorkflowCore host, ...) are
/// <c>IHostedService</c>s whose constructors reach that factory, and the generic host resolves every
/// <c>IHostedService</c>'s constructor graph at <c>Host.StartAsync</c> — before Umbraco's runtime
/// level is known. A genuinely unconfigured connection string (a fresh, not-yet-installed site; an
/// ephemeral CI boot serving only <c>swagger.json</c>) must not throw here, unlike
/// <see cref="AutomateSchemaInitializer"/>, which only ever resolves once <c>RuntimeLevel.Run</c> is
/// confirmed. Falling back to <see cref="DatabaseConnectionInfo.PlaceholderConnection"/> defers the
/// failure instead: nothing reads or writes through the resulting context before
/// <see cref="AutomateReadinessSignal"/> is signalled, which never happens below that level.
/// See <see href="https://github.com/umbraco/Umbraco.Automate/issues/226"/>.
/// </para>
/// </remarks>
internal static class AutomatePooledDbContextOptions
{
    internal static void Configure(
        IServiceProvider serviceProvider,
        DbContextOptionsBuilder options,
        string? cmsConnectionString,
        string? cmsProviderName)
    {
        string connectionString;
        string providerName;
        try
        {
            (connectionString, providerName) = DatabaseConnectionInfo.Resolve(
                serviceProvider.GetRequiredService<IOptionsMonitor<ConnectionStrings>>(),
                serviceProvider.GetRequiredService<IConfiguration>());
        }
        catch (InvalidOperationException ex)
        {
            (connectionString, providerName) = DatabaseConnectionInfo.PlaceholderConnection;
            serviceProvider.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Umbraco.Automate.Extensions.UmbracoBuilderExtensions")
                .LogWarning(
                    ex,
                    "Umbraco Automate has no database connection string configured yet. Automate will " +
                    "remain inactive until one becomes available and the site reaches RuntimeLevel.Run.");
        }

        UmbracoAutomateDbContext.ConfigureProvider(options, connectionString, providerName);

        // Defer writes on this (DI-resolved) context until Automate's own startup migrations
        // have completed. The migration handler's own standalone context is configured
        // separately, via the same ConfigureProvider call, and is deliberately not gated here.
        options.AddInterceptors(new AutomateReadinessInterceptor(
            serviceProvider.GetRequiredService<AutomateReadinessSignal>()));
    }
}
