namespace Umbraco.Automate.Persistence.Versioning;

/// <summary>
/// EF Core entity for the entity version table.
/// </summary>
internal sealed class EntityVersionEntity
{
    public Guid Id { get; set; }

    public Guid EntityId { get; set; }

    public required string EntityType { get; set; }

    public int Version { get; set; }

    public required string Snapshot { get; set; }

    public DateTime DateCreated { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public string? ChangeDescription { get; set; }
}
