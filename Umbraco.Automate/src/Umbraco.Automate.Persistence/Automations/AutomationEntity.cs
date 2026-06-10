namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// EF Core entity for the automation table.
/// </summary>
internal sealed class AutomationEntity
{
    public Guid Id { get; set; }

    public required string Alias { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public int Status { get; set; }

    public int? PublishedVersion { get; set; }

    public Guid WorkspaceId { get; set; }

    public Guid? GroupId { get; set; }

    /// <summary>
    /// Trigger, steps, connections, and canvas state serialised as a single JSON document.
    /// </summary>
    public string? Definition { get; set; }

    public int Version { get; set; }

    public DateTime DateCreated { get; set; }

    public DateTime DateModified { get; set; }

    public Guid? CreatedByUserId { get; set; }

    public Guid? ModifiedByUserId { get; set; }
}
