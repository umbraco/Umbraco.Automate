namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Thrown when an automation fails publish-time validation.
/// Contains a list of specific validation errors.
/// </summary>
public sealed class AutomationValidationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutomationValidationException"/> class.
    /// </summary>
    public AutomationValidationException(string message, IReadOnlyList<string> errors)
        : base(message)
    {
        Errors = errors;
    }

    /// <summary>
    /// Gets the individual validation errors.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }
}
