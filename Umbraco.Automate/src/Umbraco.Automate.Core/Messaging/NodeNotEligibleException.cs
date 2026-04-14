namespace Umbraco.Automate.Core.Messaging;

/// <summary>
/// Thrown by a message handler when this node was eligible at claim-time but became
/// ineligible (e.g. server role flipped) before <c>HandleAsync</c> could run. The
/// dispatcher's normal retry/backoff handles this — the message is left in <c>Failed</c>
/// state with a future <c>NextRetryUtc</c>, and a subsequently-eligible poll picks it up.
/// </summary>
/// <remarks>
/// This is a defensive safety net for the rare race between
/// <see cref="IMessageHandler.CanProcessNow"/> and <see cref="IMessageHandler.HandleAsync"/>.
/// Steady-state eligibility filtering happens in the dispatcher's poll loop.
/// </remarks>
internal sealed class NodeNotEligibleException : Exception
{
    public NodeNotEligibleException(string topic)
        : base($"Node became ineligible to process topic '{topic}' between claim and handle.")
    {
    }
}
