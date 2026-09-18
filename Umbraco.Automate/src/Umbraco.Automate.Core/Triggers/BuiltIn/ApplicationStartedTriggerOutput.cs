namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="ApplicationStartedTrigger"/> when the application starts.
/// </summary>
public sealed class ApplicationStartedTriggerOutput
{
    /// <summary>
    /// Gets the UTC timestamp when the application started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }
}
