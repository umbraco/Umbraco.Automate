namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core entity for a WorkflowCore event.
/// </summary>
internal sealed class EventEntity
{
    public required string Id { get; set; }

    public required string EventName { get; set; }

    public required string EventKey { get; set; }

    public string? EventData { get; set; }

    public DateTime EventTime { get; set; }

    public bool IsProcessed { get; set; }
}
