namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="RelationSavedTrigger"/> for each saved relation.
/// </summary>
public sealed class RelationSavedTriggerOutput
{
    /// <summary>
    /// Gets the relation's unique key.
    /// </summary>
    /// <remarks>
    /// For automatic relations (<see cref="IsAutomatic"/> is <c>true</c>) Umbraco builds
    /// fresh relation objects after a bulk insert, so this key does not match the key of
    /// the stored relation.
    /// </remarks>
    public Guid Key { get; init; }

    /// <summary>
    /// Gets the relation type's unique key.
    /// </summary>
    public Guid RelationTypeKey { get; init; }

    /// <summary>
    /// Gets the child entity's ID.
    /// </summary>
    public int ChildId { get; init; }

    /// <summary>
    /// Gets the child entity's unique key, or <c>null</c> when it could not be resolved.
    /// </summary>
    public Guid? ChildKey { get; init; }

    /// <summary>
    /// Gets the parent entity's ID.
    /// </summary>
    public int ParentId { get; init; }

    /// <summary>
    /// Gets the parent entity's unique key, or <c>null</c> when it could not be resolved.
    /// </summary>
    public Guid? ParentKey { get; init; }

    /// <summary>
    /// Gets the child entity's object type key.
    /// </summary>
    public Guid ChildObjectTypeKey { get; init; }

    /// <summary>
    /// Gets the parent entity's object type key.
    /// </summary>
    public Guid ParentObjectTypeKey { get; init; }

    /// <summary>
    /// Gets the comment associated with the relation.
    /// </summary>
    public string? Comment { get; init; }

    /// <summary>
    /// Gets a value indicating whether this save represents a newly-created relation.
    /// True when <c>CreateDate == UpdateDate</c> on the saved entity. Soft signal —
    /// database date precision may vary, so downstream automations needing a hard
    /// guarantee should re-fetch.
    /// </summary>
    public bool IsNew { get; init; }

    /// <summary>
    /// Gets a value indicating whether the relation was saved automatically by Umbraco
    /// as a side-effect of saving content, media or members (reference tracking),
    /// rather than explicitly via the relation service.
    /// </summary>
    public bool IsAutomatic { get; init; }
}
