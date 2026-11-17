using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for <see cref="GetConnectionsAction"/>.
/// </summary>
public sealed class GetConnectionsSettings
{
    /// <summary>
    /// Gets or sets the connection type alias to filter on (e.g. <c>slack</c>). Empty returns
    /// every connection the automation's workspace may use, whatever its type.
    /// </summary>
    [Field(
        Label = "Connection type",
        Description = "Only return connections of this type. Leave empty to return all connections available in this workspace.",
        EditorUiAlias = "Umb.Automate.ConnectionTypePicker")]
    public string? ConnectionType { get; set; }
}
