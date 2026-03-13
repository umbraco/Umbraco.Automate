namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// EF Core entity for the automation group table.
/// </summary>
internal sealed class AutomationGroupEntity
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public Guid? ParentId { get; set; }

    public Guid WorkspaceId { get; set; }

    public DateTime DateCreated { get; set; }
}
