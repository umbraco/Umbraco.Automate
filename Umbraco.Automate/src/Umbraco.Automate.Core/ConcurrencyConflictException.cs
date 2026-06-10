namespace Umbraco.Automate.Core;

/// <summary>
/// Thrown when a save operation fails because the entity was modified by another request
/// since it was last read (optimistic concurrency violation).
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class.
    /// </summary>
    public ConcurrencyConflictException(string entityType, Guid entityId)
        : base($"The {entityType} '{entityId}' was modified by another request. Reload and try again.")
    {
        EntityType = entityType;
        EntityId = entityId;
    }

    /// <summary>
    /// Gets the type name of the conflicting entity.
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// Gets the ID of the conflicting entity.
    /// </summary>
    public Guid EntityId { get; }
}
