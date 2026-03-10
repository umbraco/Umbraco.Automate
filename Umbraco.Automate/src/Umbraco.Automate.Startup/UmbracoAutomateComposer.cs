using Microsoft.Extensions.DependencyInjection;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Expressions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Extensions;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Automate.Startup;

/// <summary>
/// Composer that registers Umbraco Automate services with the DI container.
/// </summary>
public class UmbracoAutomateComposer : IComposer
{
    /// <inheritdoc />
    public void Compose(IUmbracoBuilder builder)
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

        // EF Core persistence (DbContext + repositories)
        builder.AddUmbracoAutomatePersistence();

        // HTTP client for HttpRequestAction
        builder.Services.AddHttpClient("UmbracoAutomate");
    }
}
