namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Represents the result of comparing two versions of an entity.
/// </summary>
public sealed class VersionComparison
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VersionComparison"/> class.
    /// </summary>
    public VersionComparison(
        Guid entityId,
        string entityType,
        int fromVersion,
        int toVersion,
        IReadOnlyList<ValueChange> changes)
    {
        EntityId = entityId;
        EntityType = entityType;
        FromVersion = fromVersion;
        ToVersion = toVersion;
        Changes = changes;
    }

    /// <summary>
    /// Gets the ID of the entity being compared.
    /// </summary>
    public Guid EntityId { get; }

    /// <summary>
    /// Gets the type of the entity being compared.
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// Gets the older version number.
    /// </summary>
    public int FromVersion { get; }

    /// <summary>
    /// Gets the newer version number.
    /// </summary>
    public int ToVersion { get; }

    /// <summary>
    /// Gets the list of value changes between the versions.
    /// </summary>
    public IReadOnlyList<ValueChange> Changes { get; }
}
