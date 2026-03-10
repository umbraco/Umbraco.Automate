namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core entity for a WorkflowCore scheduled command.
/// </summary>
internal sealed class ScheduledCommandEntity
{
    public long Id { get; set; }

    public required string CommandName { get; set; }

    public required string Data { get; set; }

    public DateTime ExecuteTime { get; set; }
}
