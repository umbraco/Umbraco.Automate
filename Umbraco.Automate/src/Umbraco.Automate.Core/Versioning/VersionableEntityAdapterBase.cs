using Umbraco.Automate.Core.Models;

namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Base class for versionable entity adapters that provides strongly-typed methods.
/// </summary>
/// <typeparam name="TEntity">The entity type this adapter manages.</typeparam>
public abstract class VersionableEntityAdapterBase<TEntity> : IVersionableEntityAdapter
    where TEntity : class, IVersionableEntity
{
    /// <inheritdoc />
    public virtual string EntityTypeName => typeof(TEntity).Name;

    /// <inheritdoc />
    public Type EntityType => typeof(TEntity);

    /// <inheritdoc />
    string IVersionableEntityAdapter.CreateSnapshot(object entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        if (entity is not TEntity typed)
        {
            throw new ArgumentException(
                $"Expected entity of type {typeof(TEntity).Name} but got {entity.GetType().Name}",
                nameof(entity));
        }

        return CreateSnapshot(typed);
    }

    /// <inheritdoc />
    object? IVersionableEntityAdapter.RestoreFromSnapshot(string json)
        => RestoreFromSnapshot(json);

    /// <inheritdoc />
    IReadOnlyList<ValueChange> IVersionableEntityAdapter.CompareVersions(object from, object to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        if (from is not TEntity typedFrom)
        {
            throw new ArgumentException(
                $"Expected entity of type {typeof(TEntity).Name} but got {from.GetType().Name}",
                nameof(from));
        }

        if (to is not TEntity typedTo)
        {
            throw new ArgumentException(
                $"Expected entity of type {typeof(TEntity).Name} but got {to.GetType().Name}",
                nameof(to));
        }

        return CompareVersions(typedFrom, typedTo);
    }

    /// <inheritdoc />
    async Task<object?> IVersionableEntityAdapter.GetEntityAsync(Guid entityId, CancellationToken cancellationToken)
        => await GetEntityAsync(entityId, cancellationToken);

    /// <summary>
    /// Creates a JSON snapshot of the entity for version storage.
    /// </summary>
    protected abstract string CreateSnapshot(TEntity entity);

    /// <summary>
    /// Restores an entity from a JSON snapshot.
    /// </summary>
    protected abstract TEntity? RestoreFromSnapshot(string json);

    /// <summary>
    /// Compares two entity versions and returns the list of value changes.
    /// </summary>
    protected abstract IReadOnlyList<ValueChange> CompareVersions(TEntity from, TEntity to);

    /// <inheritdoc />
    public abstract Task RollbackAsync(Guid entityId, int version, Guid? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of an entity from the main entity table.
    /// </summary>
    protected abstract Task<TEntity?> GetEntityAsync(Guid entityId, CancellationToken cancellationToken);

    /// <inheritdoc />
    public abstract Task<IReadOnlyCollection<ProtectedVersion>> GetProtectedVersionsAsync(CancellationToken cancellationToken = default);
}
