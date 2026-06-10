namespace Umbraco.Automate.Core.Models;

/// <summary>
/// Base interface for all Automate entities with identity.
/// </summary>
public interface IAutomateEntity
{
    /// <summary>
    /// Gets the unique identifier.
    /// </summary>
    Guid Id { get; }
}

/// <summary>
/// An entity that tracks creation and modification timestamps.
/// </summary>
public interface IAuditableEntity : IAutomateEntity
{
    /// <summary>
    /// Gets the UTC date and time the entity was created.
    /// </summary>
    DateTime DateCreated { get; }

    /// <summary>
    /// Gets the UTC date and time the entity was last modified.
    /// </summary>
    DateTime DateModified { get; }

    /// <summary>
    /// Gets the user key of the creator, or null for system-initiated entities.
    /// </summary>
    Guid? CreatedByUserId { get; }

    /// <summary>
    /// Gets the user key of the last modifier, or null for system-initiated changes.
    /// </summary>
    Guid? ModifiedByUserId { get; }
}

/// <summary>
/// An entity that supports versioning with append-only snapshots.
/// </summary>
public interface IVersionableEntity : IAuditableEntity
{
    /// <summary>
    /// Gets the current version number (starts at 1).
    /// </summary>
    int Version { get; }
}

/// <summary>
/// A versionable entity that supports a draft/published lifecycle.
/// </summary>
public interface IPublishableEntity : IVersionableEntity
{
    /// <summary>
    /// Gets the currently published version number, or null if never published.
    /// </summary>
    int? PublishedVersion { get; }
}
