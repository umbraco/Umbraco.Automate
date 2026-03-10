using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Actions.Middleware;

/// <summary>
/// Builder for the action middleware collection. Uses ordered collection to control pipeline order.
/// </summary>
public class ActionMiddlewareCollectionBuilder
    : OrderedCollectionBuilderBase<ActionMiddlewareCollectionBuilder, ActionMiddlewareCollection, IActionMiddleware>
{
    /// <inheritdoc />
    protected override ActionMiddlewareCollectionBuilder This => this;
}

/// <summary>
/// The ordered collection of action middleware.
/// </summary>
public sealed class ActionMiddlewareCollection : BuilderCollectionBase<IActionMiddleware>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionMiddlewareCollection"/> class.
    /// </summary>
    public ActionMiddlewareCollection(Func<IEnumerable<IActionMiddleware>> items) : base(items) { }
}
