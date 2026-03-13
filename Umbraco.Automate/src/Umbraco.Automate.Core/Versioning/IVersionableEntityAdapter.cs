namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Defines how a specific entity type participates in the unified versioning system.
/// </summary>
/// <remarks>
/// Each versionable entity type implements this interface to provide entity-specific
/// snapshot creation, restoration, and comparison logic. Implementations are registered via the
/// <see cref="VersionableEntityAdapterCollectionBuilder"/>.
/// </remarks>
public interface IVersionableEntityAdapter
{
    /// <summary>
    /// Gets the entity type name used as a discriminator in the unified version table.
    /// </summary>
    /// <remarks>
    /// This should be a short, stable identifier like "Automation".
    /// It is stored in the database and used to route version operations to the correct handler.
    /// </remarks>
    string EntityTypeName { get; }

    /// <summary>
    /// Gets the CLR type of the entity this adapter manages.
    /// </summary>
    Type EntityType { get; }

    /// <summary>
    /// Creates a JSON snapshot of the entity for version storage.
    /// </summary>
    /// <param name="entity">The entity to snapshot.</param>
    /// <returns>JSON string representing the entity state.</returns>
    string CreateSnapshot(object entity);

    /// <summary>
    /// Restores an entity from a JSON snapshot.
    /// </summary>
    /// <param name="json">The JSON snapshot.</param>
    /// <returns>The restored entity, or null if restoration fails.</returns>
    object? RestoreFromSnapshot(string json);

    /// <summary>
    /// Restores identity fields (e.g. Id) that are lost during JSON round-trip
    /// because they use non-public setters.
    /// </summary>
    /// <param name="entity">The deserialized entity.</param>
    /// <param name="entityId">The known entity identifier.</param>
    void HydrateIdentity(object entity, Guid entityId);

    /// <summary>
    /// Compares two entity versions and returns the list of value changes.
    /// </summary>
    /// <param name="from">The older entity version.</param>
    /// <param name="to">The newer entity version.</param>
    /// <returns>A list of value changes between the versions.</returns>
    IReadOnlyList<ValueChange> CompareVersions(object from, object to);

    /// <summary>
    /// Rolls back an entity to a previous version.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="version">The version number to rollback to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <remarks>
    /// Rollback creates a new version with the state from the target version.
    /// The implementation should delegate to the entity's service for proper save logic.
    /// </remarks>
    Task RollbackAsync(Guid entityId, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of an entity from the main entity table.
    /// </summary>
    /// <param name="entityId">The unique identifier of the entity.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The entity if found; otherwise, null.</returns>
    /// <remarks>
    /// This retrieves the live entity state, which may not yet have a snapshot in the version history table.
    /// </remarks>
    Task<object?> GetEntityAsync(Guid entityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all version numbers that are currently protected from cleanup for this entity type.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// A collection of (EntityId, Version) pairs that must not be deleted during version cleanup.
    /// For example, the published version of an automation should be protected.
    /// </returns>
    /// <remarks>
    /// The version cleanup system calls this method on all registered adapters before performing
    /// any deletions. Protected versions are excluded from both age-based and count-based cleanup.
    /// </remarks>
    Task<IReadOnlyCollection<ProtectedVersion>> GetProtectedVersionsAsync(CancellationToken cancellationToken = default);
}
