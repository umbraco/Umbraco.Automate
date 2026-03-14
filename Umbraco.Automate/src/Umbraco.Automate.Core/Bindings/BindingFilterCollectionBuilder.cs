using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Expressions;

/// <summary>
/// Builder for the expression filter collection. Supports auto-discovery and manual registration.
/// </summary>
public class ExpressionFilterCollectionBuilder
    : LazyCollectionBuilderBase<ExpressionFilterCollectionBuilder, ExpressionFilterCollection, IExpressionFilter>
{
    /// <inheritdoc />
    protected override ExpressionFilterCollectionBuilder This => this;
}

/// <summary>
/// The collection of all registered expression filters.
/// </summary>
public sealed class ExpressionFilterCollection : BuilderCollectionBase<IExpressionFilter>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExpressionFilterCollection"/> class.
    /// </summary>
    public ExpressionFilterCollection(Func<IEnumerable<IExpressionFilter>> items) : base(items) { }
}
