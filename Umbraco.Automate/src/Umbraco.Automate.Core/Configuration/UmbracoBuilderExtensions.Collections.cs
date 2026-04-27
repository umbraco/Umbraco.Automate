using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Cms;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.HealthChecks;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Validation;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Scheduling;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;
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
        builder.Services.Configure<ScheduledTriggerOptions>(
            builder.Config.GetSection("Umbraco:Automate:ScheduledTrigger"));
        builder.Services.Configure<RateLimitingOptions>(
            builder.Config.GetSection("Umbraco:Automate:RateLimiting"));

        // Collection builders — triggers, actions, connections, filters auto-discovered
        builder.AutomateTriggers()
            .Add(() => builder.TypeLoader.GetTypesWithAttribute<ITrigger, TriggerAttribute>(cache: true));
        builder.AutomateActions()
            .Add(() => builder.TypeLoader.GetTypesWithAttribute<IAction, ActionAttribute>(cache: true));
        builder.AutomateControlFlow()
            .Add(() => builder.TypeLoader.GetTypesWithAttribute<IControlFlow, ControlFlowAttribute>(cache: true));
        builder.AutomateConnectionTypes()
            .Add(() => builder.TypeLoader.GetTypesWithAttribute<IConnectionType, ConnectionTypeAttribute>(cache: true));
        builder.AutomateNotificationChannels()
            .Add(() => builder.TypeLoader.GetTypesWithAttribute<INotificationChannel, NotificationChannelAttribute>(cache: true));
        builder.AutomateWebhookAuthenticators()
            .Add<PlainSecretWebhookAuthenticator>()
            .Add<HmacSha256WebhookAuthenticator>();
        builder.AutomateBindingFilters();
        builder.AutomateVersionableEntityAdapters()
            .Add<AutomationVersionableEntityAdapter>()
            .Add<WorkspaceVersionableEntityAdapter>()
            .Add<ConnectionVersionableEntityAdapter>();

        // Wire notification triggers → TriggerNotificationHandler<T> for each notification type
        builder.RegisterTriggerNotificationHandlers();

        // Trigger subscription cache — lets the notification handler skip outbox writes for
        // triggers that no published+enabled automation listens for. Invalidated by lifecycle
        // notifications below.
        builder.Services.AddSingleton<TriggerSubscriptionRegistry>();
        builder.Services.AddSingleton<ITriggerSubscriptionRegistry>(sp => sp.GetRequiredService<TriggerSubscriptionRegistry>());
        builder.AddNotificationAsyncHandler<AutomationSavedNotification, TriggerSubscriptionInvalidator>();
        builder.AddNotificationAsyncHandler<AutomationPublishedNotification, TriggerSubscriptionInvalidator>();
        builder.AddNotificationAsyncHandler<AutomationUnpublishedNotification, TriggerSubscriptionInvalidator>();
        builder.AddNotificationAsyncHandler<AutomationDeletedNotification, TriggerSubscriptionInvalidator>();

        // Wire run-completed notification → notification channel dispatcher
        builder.AddNotificationAsyncHandler<AutomationRunCompletedNotification, RunCompletedNotificationDispatcher>();

        // Action middleware — ordered pipeline:
        //   1. ErrorHandling — catches exceptions from everything inside
        //   2. BackOfficeIdentity — resolves the workspace service principal so all
        //      subsequent middleware and the action itself execute as that identity
        //   3. StepRunLogging — logs execution timing (excludes identity resolution overhead)
        //   4. SettingsValidation — validates before audit so invalid configs aren't trailed
        //   5. AuditTrail — writes CMS audit entry on success
        builder.AutomateActionMiddleware()
            .Append<ErrorHandlingMiddleware>()
            .Append<BackOfficeIdentityMiddleware>()
            .Append<StepRunLoggingMiddleware>()
            .Append<SettingsValidationMiddleware>()
            .Append<AuditTrailMiddleware>();

        // Security — decorate Umbraco's IBackOfficeSecurityAccessor so that during
        // automation runs the AsyncLocal-based service principal identity takes precedence,
        // while outside of runs the original accessor is used unchanged.
        builder.Services.DecorateBackOfficeSecurityAccessor();
        builder.Services.AddSingleton<ISensitiveFieldProtector, SensitiveFieldProtector>();

        // Settings infrastructure
        builder.Services.AddSingleton<IEditableModelSerializer, EditableModelSerializer>();
        builder.Services.AddSingleton<IEditableModelResolver, EditableModelResolver>();
        builder.Services.AddSingleton<ActionInfrastructure>();
        builder.Services.AddSingleton<TriggerInfrastructure>();
        builder.Services.AddSingleton<ControlFlowInfrastructure>();
        builder.Services.AddSingleton<ConnectionTypeInfrastructure>();
        builder.Services.AddSingleton<NotificationChannelInfrastructure>();

        // Versioning
        builder.Services.AddSingleton<IEntityVersionService, EntityVersionService>();
        builder.Services.AddHostedService<VersionCleanupBackgroundJob>();
        builder.Services.AddHostedService<RunCleanupBackgroundJob>();
        builder.Services.AddHostedService<OutboxCleanupBackgroundJob>();
        builder.Services.AddHostedService<ScheduledTriggerBackgroundJob>();

        // Readiness signal — set by migration handler, awaited by background services
        builder.Services.AddSingleton<AutomateReadinessSignal>();

        // Diagnostics / metrics
        builder.Services.AddSingleton<AutomateMetrics>();

        // CMS integration helpers
        builder.Services.AddSingleton<IContentValueNormaliser, ContentValueNormaliser>();

        // Core services
        builder.Services.AddSingleton<IWorkspaceService, WorkspaceService>();
        builder.Services.AddSingleton<IConnectionService, ConnectionService>();
        builder.Services.AddSingleton<ISensitiveSettingsStripper, SensitiveSettingsStripper>();
        builder.Services.AddSingleton<IAutomationService, AutomationService>();
        builder.Services.AddSingleton<IWorkspaceGroupService, WorkspaceGroupService>();
        builder.Services.AddSingleton<IAutomationRunService, AutomationRunService>();
        builder.Services.AddSingleton<ActionMiddlewarePipeline>();
        builder.Services.AddSingleton<BindingEvaluator>();
        builder.Services.AddSingleton<SettingsBindingResolver>();
        builder.Services.AddSingleton<ConditionEvaluator>();

        // HTTP client for HttpRequestAction — with SSRF protection
        builder.Services.AddHttpClient("UmbracoAutomate")
            .ConfigurePrimaryHttpMessageHandler(_ => SsrfProtectionHandler.Create());

        // Outbox messaging — IOutbox + IOutboxStore registered by Persistence layer
        builder.Services.AddSingleton<IOutbox, DatabaseOutbox>();
        builder.Services.AddHostedService<OutboxDispatcher>();

        // Health check infrastructure
        builder.Services.AddSingleton<RunCleanupTracker>();
        builder.Services.AddSingleton<IAutomateHealthService, AutomateHealthService>();

        // Trigger dispatch via outbox
        builder.Services.AddSingleton<ITriggerDispatcher, OutboxTriggerDispatcher>();
        builder.Services.AddSingleton<IMessageHandler, TriggerEventHandler>();

        // Rate limiting
        builder.Services.AddSingleton<IRateLimitService, RateLimitService>();

        // Automation execution
        builder.Services.AddSingleton<IExecutionContextAccessor, ExecutionContextAccessor>();
        builder.Services.AddSingleton<IExecutionNodeEligibility, ExecutionNodeEligibility>();
        builder.Services.AddSingleton<IWorkflowCompiler, WorkflowCompiler>();
        builder.Services.AddSingleton<IAutomationExecutor, AutomationExecutor>();
        builder.Services.AddSingleton<IStepErrorClassifier, DefaultStepErrorClassifier>();
        builder.Services.AddSingleton<RunFinalizer>();

        // WorkflowCore engine with outbox-backed queue.
        builder.Services.AddSingleton<OutboxQueueProvider>();
        builder.Services.AddSingleton<IQueueProvider>(sp => sp.GetRequiredService<OutboxQueueProvider>());
        builder.Services.AddSingleton<IMessageHandler, WorkflowQueueHandler>();
        builder.Services.AddSingleton<IMessageHandler, EventQueueHandler>();
        builder.Services.AddWorkflow();
        builder.Services.AddSingleton<WorkflowDefinitionRecovery>();
        builder.Services.AddHostedService<WorkflowHostLifecycle>();

        // Startup validation
        builder.Services.AddHostedService<AutomationStartupValidator>();

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
    /// Gets the control flow collection builder. Control flow types are auto-discovered.
    /// </summary>
    public static ControlFlowCollectionBuilder AutomateControlFlow(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<ControlFlowCollectionBuilder>();

    /// <summary>
    /// Gets the binding filter collection builder. Filters are auto-discovered.
    /// </summary>
    public static BindingFilterCollectionBuilder AutomateBindingFilters(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<BindingFilterCollectionBuilder>();

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
    /// Gets the notification channel collection builder. Channels are auto-discovered.
    /// </summary>
    public static NotificationChannelCollectionBuilder AutomateNotificationChannels(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<NotificationChannelCollectionBuilder>();

    /// <summary>
    /// Gets the webhook authenticator collection builder. Add custom authenticators for
    /// provider-specific webhook validation (e.g. GitHub, Stripe).
    /// </summary>
    public static WebhookAuthenticatorCollectionBuilder AutomateWebhookAuthenticators(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<WebhookAuthenticatorCollectionBuilder>();

    /// <summary>
    /// Gets the versionable entity adapter collection builder.
    /// </summary>
    public static VersionableEntityAdapterCollectionBuilder AutomateVersionableEntityAdapters(this IUmbracoBuilder builder)
        => builder.WithCollectionBuilder<VersionableEntityAdapterCollectionBuilder>();
}
