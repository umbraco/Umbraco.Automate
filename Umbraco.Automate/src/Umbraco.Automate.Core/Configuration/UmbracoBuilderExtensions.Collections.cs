using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Expressions;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.DependencyInjection;
using WorkflowCore.Interface;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Extension methods for <see cref="IUmbracoBuilder"/> for Automate core services.
/// </summary>
public static partial class UmbracoBuilderExtensions
{
    /// <summary>
    /// Adds Umbraco Automate core services and collection builders.
    /// </summary>
    internal static IUmbracoBuilder AddUmbracoAutomateCore(this IUmbracoBuilder builder)
    {
        // Configuration options
        builder.Services.Configure<AutomateOptions>(
            builder.Config.GetSection("Umbraco:Automate"));
        builder.Services.Configure<ExecutionOptions>(
            builder.Config.GetSection("Umbraco:Automate:Execution"));
        builder.Services.Configure<OutboxOptions>(
            builder.Config.GetSection("Umbraco:Automate:Outbox"));
        builder.Services.Configure<VersionCleanupPolicy>(
            builder.Config.GetSection("Umbraco:Automate:VersionCleanup"));
        builder.Services.Configure<RunCleanupPolicy>(
            builder.Config.GetSection("Umbraco:Automate:RunCleanup"));
        builder.Services.Configure<WebhookOptions>(
            builder.Config.GetSection("Umbraco:Automate:Webhook"));

        // Collection builders — triggers, actions, connections, filters (auto-discovered via TypeLoader)
        builder.AutomateTriggers()
            .Add(() => builder.TypeLoader.GetTypesWithAttribute<ITrigger, TriggerAttribute>(cache: true));
        builder.AutomateActions()
            .Add(() => builder.TypeLoader.GetTypesWithAttribute<IAction, ActionAttribute>(cache: true));
        builder.AutomateConnectionTypes()
            .Add(() => builder.TypeLoader.GetTypesWithAttribute<IConnectionType, ConnectionTypeAttribute>(cache: true));
        builder.AutomateExpressionFilters();
        builder.AutomateVersionableEntityAdapters()
            .Add<AutomationVersionableEntityAdapter>();

        // Wire notification triggers → TriggerNotificationHandler<T> for each notification type
        builder.RegisterTriggerNotificationHandlers();

        // Action middleware — ordered pipeline
        builder.AutomateActionMiddleware()
            .Append<ErrorHandlingMiddleware>()
            .Append<StepRunLoggingMiddleware>();

        // Security
        builder.Services.AddSingleton<ISensitiveFieldProtector, SensitiveFieldProtector>();

        // Settings infrastructure
        builder.Services.AddSingleton<IEditableModelSerializer, EditableModelSerializer>();
        builder.Services.AddSingleton<IEditableModelResolver, EditableModelResolver>();
        builder.Services.AddSingleton<ActionInfrastructure>();
        builder.Services.AddSingleton<TriggerInfrastructure>();
        builder.Services.AddSingleton<ConnectionTypeInfrastructure>();

        // Versioning
        builder.Services.AddSingleton<IEntityVersionService, EntityVersionService>();
        builder.Services.AddHostedService<VersionCleanupBackgroundJob>();
        builder.Services.AddHostedService<RunCleanupBackgroundJob>();

        // Diagnostics / metrics
        builder.Services.AddSingleton<AutomateMetrics>();

        // Core services
        builder.Services.AddSingleton<IWorkspaceService, WorkspaceService>();
        builder.Services.AddSingleton<IConnectionService, ConnectionService>();
        builder.Services.AddSingleton<IAutomationService, AutomationService>();
        builder.Services.AddSingleton<IAutomationRunService, AutomationRunService>();
        builder.Services.AddSingleton<ActionMiddlewarePipeline>();
        builder.Services.AddSingleton<ExpressionEvaluator>();

        // HTTP client for HttpRequestAction — with SSRF protection
        builder.Services.AddHttpClient("UmbracoAutomate")
            .ConfigurePrimaryHttpMessageHandler(_ => SsrfProtectionHandler.Create());

        // Outbox messaging — IOutbox + IOutboxStore registered by Persistence layer
        builder.Services.AddSingleton<IOutbox, DatabaseOutbox>();
        builder.Services.AddHostedService<OutboxDispatcher>();

        // Health checks
        builder.Services.AddHealthChecks()
            .AddCheck<OutboxHealthCheck>("umbraco-automate-outbox", tags: ["automate"]);

        // Trigger dispatch via outbox
        builder.Services.AddSingleton<ITriggerDispatcher, OutboxTriggerDispatcher>();
        builder.Services.AddSingleton<IMessageHandler, TriggerEventHandler>();

        // Automation execution
        builder.Services.AddSingleton<IExecutionContextAccessor, ExecutionContextAccessor>();
        builder.Services.AddSingleton<IAutomationExecutor, AutomationExecutor>();

        // WorkflowCore engine with outbox-backed queue
        builder.Services.AddSingleton<OutboxQueueProvider>();
        builder.Services.AddSingleton<IQueueProvider>(sp => sp.GetRequiredService<OutboxQueueProvider>());
        builder.Services.AddSingleton<IMessageHandler, WorkflowQueueHandler>();
        builder.Services.AddSingleton<IMessageHandler, EventQueueHandler>();
        builder.Services.AddWorkflow();

        return builder;
    }

    /// <summary>
    /// Gets the trigger collection builder. Triggers are auto-discovered.
    /// </summary>
    public static TriggerCollectionBuilder AutomateTriggers(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<TriggerCollectionBuilder>();

    /// <summary>
    /// Gets the action collection builder. Actions are auto-discovered.
    /// </summary>
    public static ActionCollectionBuilder AutomateActions(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<ActionCollectionBuilder>();

    /// <summary>
    /// Gets the expression filter collection builder. Filters are auto-discovered.
    /// </summary>
    public static ExpressionFilterCollectionBuilder AutomateExpressionFilters(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<ExpressionFilterCollectionBuilder>();

    /// <summary>
    /// Gets the action middleware collection builder. Use to control pipeline order.
    /// </summary>
    public static ActionMiddlewareCollectionBuilder AutomateActionMiddleware(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<ActionMiddlewareCollectionBuilder>();

    /// <summary>
    /// Gets the connection type collection builder. Connection types are auto-discovered.
    /// </summary>
    public static ConnectionTypeCollectionBuilder AutomateConnectionTypes(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<ConnectionTypeCollectionBuilder>();

    /// <summary>
    /// Gets the versionable entity adapter collection builder.
    /// </summary>
    public static VersionableEntityAdapterCollectionBuilder AutomateVersionableEntityAdapters(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<VersionableEntityAdapterCollectionBuilder>();
}
