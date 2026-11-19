namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Thrown when a step's settings cannot be resolved into their typed model, for example because
/// a field fails validation, a configuration reference is not permitted or cannot be found, or the
/// settings do not match the model's shape. Automate's own resolution messages name configuration
/// keys, not their resolved values. Messages from custom validation rules, and from inner exceptions
/// wrapped by the resolver's fallback, are passed through as written.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> so existing catch blocks keep working.
/// Callers that need to tell a settings problem apart from an unrelated bug catch this type.
/// </remarks>
public sealed class SettingsResolutionException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsResolutionException"/> class.
    /// </summary>
    public SettingsResolutionException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsResolutionException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the problem.</param>
    public SettingsResolutionException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsResolutionException"/> class.
    /// </summary>
    /// <param name="message">The message that describes the problem.</param>
    /// <param name="innerException">The exception that caused this one.</param>
    public SettingsResolutionException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
