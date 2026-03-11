using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Builder for the versionable entity adapter collection.
/// </summary>
public class VersionableEntityAdapterCollectionBuilder
    : LazyCollectionBuilderBase<VersionableEntityAdapterCollectionBuilder, VersionableEntityAdapterCollection, IVersionableEntityAdapter>
{
    /// <inheritdoc />
    protected override VersionableEntityAdapterCollectionBuilder This => this;
}

/// <summary>
/// A collection of versionable entity adapters.
/// </summary>
public sealed class VersionableEntityAdapterCollection : BuilderCollectionBase<IVersionableEntityAdapter>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionableEntityAdapterCollection"/> class.
    /// </summary>
    public VersionableEntityAdapterCollection(Func<IEnumerable<IVersionableEntityAdapter>> items)
        : base(items)
    { }

    /// <summary>
    /// Gets a versionable entity adapter by its type name.
    /// </summary>
    public IVersionableEntityAdapter? GetByTypeName(string entityTypeName)
        => this.FirstOrDefault(t => t.EntityTypeName.Equals(entityTypeName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Gets a versionable entity adapter by its CLR type.
    /// </summary>
    public IVersionableEntityAdapter? GetByType(Type entityType)
        => this.FirstOrDefault(t => t.EntityType == entityType);

    /// <summary>
    /// Gets a versionable entity adapter by its CLR type.
    /// </summary>
    public IVersionableEntityAdapter? GetByType<TEntity>()
        => GetByType(typeof(TEntity));
}
