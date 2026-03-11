using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Builder for the action collection. Supports auto-discovery of <see cref="IAction"/>
/// implementations via <see cref="ActionAttribute"/>.
/// </summary>
public class ActionCollectionBuilder
    : LazyCollectionBuilderBase<ActionCollectionBuilder, ActionCollection, IAction>
{
    /// <inheritdoc />
    protected override ActionCollectionBuilder This => this;
}

/// <summary>
/// The collection of all registered actions.
/// </summary>
public sealed class ActionCollection : BuilderCollectionBase<IAction>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionCollection"/> class.
    /// </summary>
    public ActionCollection(Func<IEnumerable<IAction>> items) : base(items) { }

    /// <summary>
    /// Gets an action by its alias.
    /// </summary>
    public IAction? GetByAlias(string alias)
        => this.FirstOrDefault(a => string.Equals(a.Alias, alias, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets an action by its alias, cast to a specific type.
    /// </summary>
    public T? GetByAlias<T>(string alias) where T : class, IAction
        => GetByAlias(alias) as T;
}
