namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Repository interface for unified entity version storage.
/// </summary>
/// <remarks>
/// This repository handles all version CRUD operations for the unified <c>umbracoAutomateEntityVersion</c> table.
/// It is an internal implementation detail of the <see cref="IEntityVersionService"/>.
/// </remarks>
internal interface IEntityVersionRepository
{
    /// <summary>
    /// Gets the version history for an entity with pagination support.
    /// </summary>
    Task<IEnumerable<EntityVersion>> GetVersionHistoryAsync(
        Guid entityId,
        string entityType,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of version records for a specific entity.
    /// </summary>
    Task<int> GetVersionCountByEntityAsync(
        Guid entityId,
        string entityType,
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
    /// Saves a new version record.
    /// </summary>
    Task SaveVersionAsync(
        Guid entityId,
        string entityType,
        int version,
        string snapshot,
        Guid? userId,
        string? changeDescription,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all versions for an entity.
    /// </summary>
    Task DeleteVersionsAsync(
        Guid entityId,
        string entityType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all versions older than the specified threshold, excluding protected versions.
    /// </summary>
    /// <param name="threshold">Versions created before this date are eligible for deletion.</param>
    /// <param name="protectedVersions">Versions that must not be deleted (e.g. published versions).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of versions deleted.</returns>
    Task<int> DeleteVersionsOlderThanAsync(
        DateTime threshold,
        IReadOnlyCollection<ProtectedVersion> protectedVersions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes excess versions per entity, keeping only the most recent N versions and excluding protected versions.
    /// </summary>
    /// <param name="maxVersionsPerEntity">Maximum number of versions to keep per entity.</param>
    /// <param name="protectedVersions">Versions that must not be deleted (e.g. published versions).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of versions deleted.</returns>
    Task<int> DeleteExcessVersionsAsync(
        int maxVersionsPerEntity,
        IReadOnlyCollection<ProtectedVersion> protectedVersions,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of version records.
    /// </summary>
    Task<int> GetVersionCountAsync(CancellationToken cancellationToken = default);
}
