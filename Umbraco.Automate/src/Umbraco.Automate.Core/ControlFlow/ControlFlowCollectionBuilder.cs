using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.ControlFlow;

/// <summary>
/// Builder for the control flow collection. Supports auto-discovery of <see cref="IControlFlow"/>
/// implementations via <see cref="ControlFlowAttribute"/>.
/// </summary>
public class ControlFlowCollectionBuilder
    : LazyCollectionBuilderBase<ControlFlowCollectionBuilder, ControlFlowCollection, IControlFlow>
{
    /// <inheritdoc />
    protected override ControlFlowCollectionBuilder This => this;
}

/// <summary>
/// The collection of all registered control flow step types.
/// </summary>
public sealed class ControlFlowCollection : BuilderCollectionBase<IControlFlow>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ControlFlowCollection"/> class.
    /// </summary>
    public ControlFlowCollection(Func<IEnumerable<IControlFlow>> items) : base(items) { }

    /// <summary>
    /// Gets a control flow step type by its alias.
    /// </summary>
    public IControlFlow? GetByAlias(string alias)
        => this.FirstOrDefault(c => string.Equals(c.Alias, alias, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets a control flow step type by its alias, cast to a specific type.
    /// </summary>
    public T? GetByAlias<T>(string alias) where T : class, IControlFlow
        => GetByAlias(alias) as T;
}
