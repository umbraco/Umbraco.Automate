namespace Umbraco.Automate.Core.Versioning;

/// <summary>
/// Identifies a specific entity version that must be protected from cleanup.
/// </summary>
/// <param name="EntityId">The unique identifier of the entity.</param>
/// <param name="Version">The version number that is protected.</param>
public readonly record struct ProtectedVersion(Guid EntityId, int Version);
