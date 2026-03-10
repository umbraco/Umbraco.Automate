namespace Umbraco.Automate.Persistence.Workflows;

/// <summary>
/// EF Core entity for a WorkflowCore event subscription.
/// </summary>
internal sealed class EventSubscriptionEntity
{
    public required string Id { get; set; }

    public required string WorkflowId { get; set; }

    public int StepId { get; set; }

    public required string ExecutionPointerId { get; set; }

    public required string EventName { get; set; }

    public required string EventKey { get; set; }

    public DateTime SubscribeAsOf { get; set; }

    public string? SubscriptionData { get; set; }

    public string? ExternalToken { get; set; }

    public string? ExternalWorkerId { get; set; }

    public DateTime? ExternalTokenExpiry { get; set; }
}
