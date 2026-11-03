namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Non-generic view over <see cref="BatchTriggerOutput{TItem}"/> so dispatch-time code can
/// inspect and narrow a batch without knowing its item type.
/// </summary>
/// <remarks>
/// Internal to Umbraco.Automate.Core: the contract between the built-in batch output and
/// the built-in node-scoped dispatch authoriser, matching the internal-only convention of
/// the per-item scope markers.
/// </remarks>
internal interface IBatchTriggerOutput
{
    /// <summary>
    /// Gets the batch items.
    /// </summary>
    IReadOnlyList<object> GetItems();

    /// <summary>
    /// Returns a new batch of the same type holding only <paramref name="items"/>, with every
    /// derived field (such as the count) recomputed from them. The current instance is not
    /// changed.
    /// </summary>
    /// <param name="items">The items to keep. Each must be of the batch's item type.</param>
    IBatchTriggerOutput WithItems(IReadOnlyList<object> items);
}
