using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Models;

namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Default implementation of <see cref="IEntityVersionService"/>.
/// </summary>
internal sealed class EntityVersionService : IEntityVersionService
{
    private readonly IEntityVersionRepository _repository;
    private readonly VersionableEntityAdapterCollection _adapters;
    private readonly IOptionsMonitor<VersionCleanupPolicy> _cleanupPolicy;

    public EntityVersionService(
        IEntityVersionRepository repository,
        VersionableEntityAdapterCollection adapters,
        IOptionsMonitor<VersionCleanupPolicy> cleanupPolicy)
    {
        _repository = repository;
        _adapters = adapters;
        _cleanupPolicy = cleanupPolicy;
    }

    /// <inheritdoc />
    public async Task<(IEnumerable<EntityVersion> Items, int Total)> GetVersionHistoryAsync(
        Guid entityId,
        string entityType,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        ValidateEntityType(entityType);

        var items = await _repository.GetVersionHistoryAsync(entityId, entityType, skip, take, cancellationToken);
        var total = await _repository.GetVersionCountByEntityAsync(entityId, entityType, cancellationToken);

        return (items, total);
    }

    /// <inheritdoc />
    public async Task<EntityVersion?> GetVersionAsync(
        Guid entityId,
        string entityType,
        int version,
        CancellationToken cancellationToken = default)
    {
        var adapter = _adapters.GetByTypeName(entityType)
            ?? throw new ArgumentException($"Unknown entity type: {entityType}", nameof(entityType));

        // First try to get from repository (historical versions).
        var versionRecord = await _repository.GetVersionAsync(entityId, entityType, version, cancellationToken);
        if (versionRecord is not null)
        {
            return versionRecord;
        }

        // If not found in repository, check if it's the current version.
        var currentEntity = await adapter.GetEntityAsync(entityId, cancellationToken);
        if (currentEntity is IVersionableEntity versionable && versionable.Version == version)
        {
            var auditable = currentEntity as IAuditableEntity;
            return new EntityVersion
            {
                Id = Guid.Empty, // Synthetic — no DB record exists.
                EntityId = entityId,
                EntityType = entityType,
                Version = version,
                Snapshot = adapter.CreateSnapshot(currentEntity),
                DateCreated = auditable?.DateModified ?? DateTime.UtcNow,
                CreatedByUserId = auditable?.ModifiedByUserId,
            };
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<TEntity?> GetVersionSnapshotAsync<TEntity>(
        Guid entityId,
        int version,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionableEntity
    {
        var adapter = GetAdapter<TEntity>();

        // First try to get from repository (historical versions).
        var versionRecord = await _repository.GetVersionAsync(entityId, adapter.EntityTypeName, version, cancellationToken);
        if (versionRecord is not null)
        {
            return adapter.RestoreFromSnapshot(versionRecord.Snapshot) as TEntity;
        }

        // If not found in repository, check if it's the current version.
        var currentEntity = await adapter.GetEntityAsync(entityId, cancellationToken);
        if (currentEntity is TEntity typedEntity && typedEntity.Version == version)
        {
            return typedEntity;
        }

        return null;
    }

    /// <inheritdoc />
    public async Task SaveVersionAsync<TEntity>(
        TEntity entity,
        Guid? userId,
        string? changeDescription = null,
        CancellationToken cancellationToken = default)
        where TEntity : class, IVersionableEntity
    {
        var adapter = GetAdapter<TEntity>();

        var entityId = entity is IAutomateEntity automateEntity
            ? automateEntity.Id
            : throw new InvalidOperationException($"Entity does not implement {nameof(IAutomateEntity)}.");

        var snapshot = adapter.CreateSnapshot(entity);

        await _repository.SaveVersionAsync(
            entityId,
            adapter.EntityTypeName,
            entity.Version,
            snapshot,
            userId,
            changeDescription,
            cancellationToken);
    }

    /// <inheritdoc />
    public Task DeleteVersionsAsync(
        Guid entityId,
        string entityType,
        CancellationToken cancellationToken = default)
    {
        ValidateEntityType(entityType);
        return _repository.DeleteVersionsAsync(entityId, entityType, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<VersionComparison?> CompareVersionsAsync(
        Guid entityId,
        string entityType,
        int fromVersion,
        int toVersion,
        CancellationToken cancellationToken = default)
    {
        var adapter = _adapters.GetByTypeName(entityType)
            ?? throw new ArgumentException($"Unknown entity type: {entityType}", nameof(entityType));

        // Get the current entity to check its version.
        var currentEntity = await adapter.GetEntityAsync(entityId, cancellationToken);
        var currentVersion = (currentEntity as IVersionableEntity)?.Version ?? 0;

        // Resolve "from" version.
        object? fromEntity;
        if (fromVersion == currentVersion && currentEntity is not null)
        {
            fromEntity = currentEntity;
        }
        else
        {
            var fromRecord = await _repository.GetVersionAsync(entityId, entityType, fromVersion, cancellationToken);
            fromEntity = fromRecord is null ? null : adapter.RestoreFromSnapshot(fromRecord.Snapshot);
        }

        // Resolve "to" version.
        object? toEntity;
        if (toVersion == currentVersion && currentEntity is not null)
        {
            toEntity = currentEntity;
        }
        else
        {
            var toRecord = await _repository.GetVersionAsync(entityId, entityType, toVersion, cancellationToken);
            toEntity = toRecord is null ? null : adapter.RestoreFromSnapshot(toRecord.Snapshot);
        }

        if (fromEntity is null || toEntity is null)
        {
            return null;
        }

        var changes = adapter.CompareVersions(fromEntity, toEntity);

        return new VersionComparison(entityId, entityType, fromVersion, toVersion, changes);
    }

    /// <inheritdoc />
    public string CreateSnapshot<TEntity>(TEntity entity)
        where TEntity : class, IVersionableEntity
    {
        var adapter = GetAdapter<TEntity>();
        return adapter.CreateSnapshot(entity);
    }

    /// <inheritdoc />
    public TEntity? RestoreFromSnapshot<TEntity>(string snapshot)
        where TEntity : class, IVersionableEntity
    {
        var adapter = GetAdapter<TEntity>();
        return adapter.RestoreFromSnapshot(snapshot) as TEntity;
    }

    /// <inheritdoc />
    public async Task<VersionCleanupResult> CleanupVersionsAsync(CancellationToken cancellationToken = default)
    {
        var policy = _cleanupPolicy.CurrentValue;

        if (!policy.Enabled)
        {
            return new VersionCleanupResult
            {
                WasSkipped = true,
                SkipReason = "Version cleanup is disabled",
                RemainingVersions = await _repository.GetVersionCountAsync(cancellationToken),
            };
        }

        if (policy is { RetentionDays: <= 0, MaxVersionsPerEntity: <= 0 })
        {
            return new VersionCleanupResult
            {
                WasSkipped = true,
                SkipReason = "No cleanup policy configured (both RetentionDays and MaxVersionsPerEntity are 0 or less)",
                RemainingVersions = await _repository.GetVersionCountAsync(cancellationToken),
            };
        }

        // Collect protected versions from all adapters (e.g. published automation versions).
        var protectedVersions = await CollectProtectedVersionsAsync(cancellationToken);

        var deletedByAge = 0;
        var deletedByCount = 0;

        // Run age-based cleanup first (if configured).
        if (policy.RetentionDays > 0)
        {
            var threshold = DateTime.UtcNow.AddDays(-policy.RetentionDays);
            deletedByAge = await _repository.DeleteVersionsOlderThanAsync(threshold, protectedVersions, cancellationToken);
        }

        // Run count-based cleanup second (if configured).
        if (policy.MaxVersionsPerEntity > 0)
        {
            deletedByCount = await _repository.DeleteExcessVersionsAsync(policy.MaxVersionsPerEntity, protectedVersions, cancellationToken);
        }

        var remainingCount = await _repository.GetVersionCountAsync(cancellationToken);

        return new VersionCleanupResult
        {
            DeletedByAge = deletedByAge,
            DeletedByCount = deletedByCount,
            RemainingVersions = remainingCount,
        };
    }

    private async Task<IReadOnlyCollection<ProtectedVersion>> CollectProtectedVersionsAsync(CancellationToken cancellationToken)
    {
        var allProtected = new List<ProtectedVersion>();

        foreach (var adapter in _adapters)
        {
            var protectedVersions = await adapter.GetProtectedVersionsAsync(cancellationToken);
            allProtected.AddRange(protectedVersions);
        }

        return allProtected;
    }

    private IVersionableEntityAdapter GetAdapter<TEntity>()
        where TEntity : class, IVersionableEntity
    {
        return _adapters.GetByType<TEntity>()
            ?? throw new InvalidOperationException($"No versionable entity adapter registered for {typeof(TEntity).Name}");
    }

    private void ValidateEntityType(string entityType)
    {
        if (_adapters.GetByTypeName(entityType) is null)
        {
            throw new ArgumentException(
                $"Unknown entity type: {entityType}. Supported types: {string.Join(", ", _adapters.Select(a => a.EntityTypeName))}",
                nameof(entityType));
        }
    }
}
