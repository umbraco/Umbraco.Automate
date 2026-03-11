namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Summary statistics for the outbox message store.
/// </summary>
internal sealed class OutboxStats
{
    public int Pending { get; init; }

    public int Processing { get; init; }

    public int Failed { get; init; }

    public int DeadLettered { get; init; }
}
