using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Builder for the connection type collection. Supports auto-discovery of <see cref="IConnectionType"/>
/// implementations via <see cref="ConnectionTypeAttribute"/>.
/// </summary>
public class ConnectionTypeCollectionBuilder
    : LazyCollectionBuilderBase<ConnectionTypeCollectionBuilder, ConnectionTypeCollection, IConnectionType>
{
    /// <inheritdoc />
    protected override ConnectionTypeCollectionBuilder This => this;
}

/// <summary>
/// The collection of all registered connection types.
/// </summary>
public sealed class ConnectionTypeCollection : BuilderCollectionBase<IConnectionType>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionTypeCollection"/> class.
    /// </summary>
    public ConnectionTypeCollection(Func<IEnumerable<IConnectionType>> items) : base(items) { }

    /// <summary>
    /// Gets a connection type by its alias.
    /// </summary>
    public IConnectionType? GetByAlias(string alias)
        => this.FirstOrDefault(c => string.Equals(c.Alias, alias, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets a connection type by its alias, cast to a specific type.
    /// </summary>
    public T? GetByAlias<T>(string alias) where T : class, IConnectionType
        => GetByAlias(alias) as T;
}
