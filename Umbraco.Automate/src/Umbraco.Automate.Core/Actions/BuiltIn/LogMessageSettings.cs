using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Settings for the <see cref="LogMessageAction"/>.
/// </summary>
public sealed class LogMessageSettings
{
    /// <summary>
    /// Gets or sets the message to log. Supports expression syntax.
    /// </summary>
    [Field(Label = "Message", Description = "The message to write to the log. Supports ${ expression } syntax.", SupportsExpressions = true)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the log level. Defaults to "Information".
    /// </summary>
    [Field(Label = "Log Level", Description = "The severity level (Debug, Information, Warning, Error).", SortOrder = 1)]
    public string LogLevel { get; set; } = "Information";
}
