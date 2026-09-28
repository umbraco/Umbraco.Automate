namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Thrown by an <see cref="IConnectionSettingsSaveHandler"/> when a connection's settings must not be saved.
/// The message is shown to the backoffice user.
/// </summary>
internal sealed class ConnectionSettingsValidationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionSettingsValidationException"/> class.
    /// </summary>
    public ConnectionSettingsValidationException(string message)
        : base(message)
    {
    }
}
