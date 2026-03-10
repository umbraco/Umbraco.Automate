using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Builder for the trigger collection. Supports auto-discovery of <see cref="ITrigger"/>
/// implementations via <see cref="TriggerAttribute"/>.
/// </summary>
public class TriggerCollectionBuilder
    : LazyCollectionBuilderBase<TriggerCollectionBuilder, TriggerCollection, ITrigger>
{
    /// <inheritdoc />
    protected override TriggerCollectionBuilder This => this;
}

/// <summary>
/// The collection of all registered triggers.
/// </summary>
public sealed class TriggerCollection : BuilderCollectionBase<ITrigger>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerCollection"/> class.
    /// </summary>
    public TriggerCollection(Func<IEnumerable<ITrigger>> items) : base(items) { }

    /// <summary>
    /// Gets a trigger by its alias.
    /// </summary>
    public ITrigger? GetByAlias(string alias)
        => this.FirstOrDefault(t => string.Equals(t.Alias, alias, StringComparison.OrdinalIgnoreCase));
}
