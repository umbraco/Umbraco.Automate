namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Generic output model for batch triggers that produce a collection of items
/// from a single notification event.
/// </summary>
/// <typeparam name="TItem">The per-item output type (reused from the corresponding per-item trigger).</typeparam>
public sealed class BatchTriggerOutput<TItem> where TItem : class
{
    /// <summary>
    /// Gets the collection of items from the notification.
    /// </summary>
    public List<TItem> Items { get; init; } = [];

    /// <summary>
    /// Gets the total number of items in the collection.
    /// </summary>
    public int Count { get; init; }
}
