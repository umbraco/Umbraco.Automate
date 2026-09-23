using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="NotifyEditorAction"/>.
/// </summary>
public sealed class NotifyEditorOutput
{
    /// <summary>
    /// Gets the key of the content item the notification was dispatched for.
    /// </summary>
    [Description("The key of the content item the notification was dispatched for.")]
    public Guid ContentKey { get; init; }
}
