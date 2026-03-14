namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Output produced by the <see cref="LogMessageAction"/>.
/// </summary>
public sealed class LogMessageOutput
{
    /// <summary>
    /// Gets the message that was logged.
    /// </summary>
    public string Message { get; init; } = string.Empty;
}
