namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// The trigger configuration within an automation definition.
/// </summary>
public sealed class TriggerConfiguration
{
    /// <summary>
    /// Gets or sets the alias of the trigger type (e.g. "contentPublished").
    /// </summary>
    public required string TriggerAlias { get; set; }

    /// <summary>
    /// Gets or sets the trigger settings as a key-value dictionary.
    /// </summary>
    public Dictionary<string, object?> Settings { get; set; } = [];
}
