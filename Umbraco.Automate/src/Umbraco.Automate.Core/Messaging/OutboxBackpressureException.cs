namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Thrown when the outbox rejects a message because the pending queue depth exceeds the configured limit.
/// </summary>
public sealed class OutboxBackpressureException : Exception
{
    public OutboxBackpressureException(int currentDepth, int maxDepth)
        : base($"Outbox backpressure: {currentDepth} pending messages exceeds limit of {maxDepth}")
    {
        CurrentDepth = currentDepth;
        MaxDepth = maxDepth;
    }

    public int CurrentDepth { get; }

    public int MaxDepth { get; }
}
