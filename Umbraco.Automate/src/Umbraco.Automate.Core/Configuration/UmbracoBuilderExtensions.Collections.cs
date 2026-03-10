using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Expressions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Triggers;
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

        // Collection builders — triggers, actions, filters auto-discovered
        builder.AutomateTriggers();
        builder.AutomateActions();
        builder.AutomateExpressionFilters();

        // Action middleware — ordered pipeline
        builder.AutomateActionMiddleware()
            .Append<ErrorHandlingMiddleware>()
            .Append<StepRunLoggingMiddleware>();

        // Core services
        builder.Services.AddSingleton<IAutomationService, AutomationService>();
        builder.Services.AddSingleton<IAutomationRunService, AutomationRunService>();
        builder.Services.AddSingleton<ActionMiddlewarePipeline>();
        builder.Services.AddSingleton<ExpressionEvaluator>();

        // HTTP client for HttpRequestAction
        builder.Services.AddHttpClient("UmbracoAutomate");

        // Trigger dispatch via CAP
        builder.Services.AddSingleton<ITriggerDispatcher, CapTriggerDispatcher>();
        builder.Services.AddTransient<TriggerEventConsumer>();

        // Automation execution
        builder.Services.AddSingleton<IAutomationExecutor, AutomationExecutor>();

        // WorkflowCore engine with custom persistence and queue
        builder.Services.AddSingleton<IPersistenceProvider, InMemoryWorkflowPersistenceProvider>();
        builder.Services.AddSingleton<CapQueueProvider>();
        builder.Services.AddSingleton<IQueueProvider>(sp => sp.GetRequiredService<CapQueueProvider>());
        builder.Services.AddWorkflow();

        // CAP — in-memory storage and transport for v1
        builder.Services.AddCap(cap =>
        {
            cap.UseInMemoryStorage();
        });

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
}
