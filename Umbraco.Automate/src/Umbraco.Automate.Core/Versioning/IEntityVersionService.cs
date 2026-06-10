using Umbraco.Automate.Core.Models;

namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Service for managing entity version snapshots.
/// </summary>
/// <remarks>
/// This service provides a single entry point for all version operations across all entity types.
/// It delegates entity-specific logic (snapshot creation, restoration, comparison) to the
/// appropriate <see cref="IVersionableEntityAdapter"/> handler.
/// </remarks>
public interface IEntityVersionService
{
    /// <summary>
    /// Gets the version history for an entity with pagination support.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="entityType">The entity type name (e.g. "Automation").</param>
    /// <param name="skip">Number of versions to skip.</param>
    /// <param name="take">Maximum number of versions to return.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A tuple containing the paginated version history (ordered by version descending) and the total count.</returns>
    Task<(IEnumerable<EntityVersion> Items, int Total)> GetVersionHistoryAsync(
        Guid entityId,
        string entityType,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific version record for an entity.
    /// </summary>
    Task<EntityVersion?> GetVersionAsync(
        Guid entityId,
        string entityType,
        int version,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific version snapshot restored to its domain model.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="version">The version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The restored entity, or null if not found.</returns>
    Task<TEntity?> GetVersionSnapshotAsync<TEntity>(
        Guid entityId,
        int version,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionableEntity;

    /// <summary>
    /// Saves a version snapshot for an entity.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <param name="entity">The entity to snapshot.</param>
    /// <param name="userId">The user who triggered the save.</param>
    /// <param name="changeDescription">Optional description of what changed.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    Task SaveVersionAsync<TEntity>(
        TEntity entity,
        Guid? userId,
        string? changeDescription = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionableEntity;

    /// <summary>
    /// Deletes all versions for an entity.
    /// </summary>
    Task DeleteVersionsAsync(
        Guid entityId,
        string entityType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Compares two versions of an entity.
    /// </summary>
    /// <param name="entityId">The entity ID.</param>
    /// <param name="entityType">The entity type name.</param>
    /// <param name="fromVersion">The older version number.</param>
    /// <param name="toVersion">The newer version number.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The comparison result, or null if either version is not found.</returns>
    Task<VersionComparison?> CompareVersionsAsync(
        Guid entityId,
        string entityType,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a snapshot of an entity without saving it.
    /// </summary>
    string CreateSnapshot<TEntity>(TEntity entity)
        where TEntity : class, IVersionableEntity;

    /// <summary>
    /// Restores an entity from a snapshot without querying the database.
    /// </summary>
    TEntity? RestoreFromSnapshot<TEntity>(string snapshot)
        where TEntity : class, IVersionableEntity;

    /// <summary>
    /// Cleans up old version records based on the configured policy.
    /// </summary>
    Task<VersionCleanupResult> CleanupVersionsAsync(CancellationToken cancellationToken = default);
}
