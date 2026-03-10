using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Expressions;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Automate.Extensions;

/// <summary>
/// Extension methods for <see cref="IUmbracoBuilder"/> for Automate collection builders.
/// </summary>
public static partial class UmbracoBuilderExtensions
{
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
