using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Bindings;

/// <summary>
/// Builder for the binding filter collection. Supports auto-discovery and manual registration.
/// </summary>
public class BindingFilterCollectionBuilder
    : LazyCollectionBuilderBase<BindingFilterCollectionBuilder, BindingFilterCollection, IBindingFilter>
{
    /// <inheritdoc />
    protected override BindingFilterCollectionBuilder This => this;
}

/// <summary>
/// The collection of all registered binding filters.
/// </summary>
public sealed class BindingFilterCollection : BuilderCollectionBase<IBindingFilter>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BindingFilterCollection"/> class.
    /// </summary>
    public BindingFilterCollection(Func<IEnumerable<IBindingFilter>> items) : base(items) { }
}
