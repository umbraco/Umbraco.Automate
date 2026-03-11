using Umbraco.Automate.Core.Messaging;

namespace Umbraco.Automate.Persistence.Outbox;

/// <summary>
/// EF Core entity for the <c>umbracoAutomateOutbox</c> table.
/// </summary>
internal sealed class OutboxMessageEntity
{
    public long Id { get; set; }

    public required string Topic { get; set; }

    public required string Body { get; set; }

    public string? IdempotencyKey { get; set; }

    public DateTime CreatedUtc { get; set; }

    public int Status { get; set; }

    public int RetryCount { get; set; }

    public DateTime? NextRetryUtc { get; set; }

    public string? Error { get; set; }

    public string? ClaimedByInstance { get; set; }

    public DateTime? ClaimedUtc { get; set; }

    internal OutboxMessage ToOutboxMessage() => new()
    {
        Id = Id,
        Topic = Topic,
        Body = Body,
        IdempotencyKey = IdempotencyKey,
        CreatedUtc = CreatedUtc,
        Status = (MessageStatus)Status,
        RetryCount = RetryCount,
        NextRetryUtc = NextRetryUtc,
        Error = Error,
        ClaimedByInstance = ClaimedByInstance,
        ClaimedUtc = ClaimedUtc,
    };
}
