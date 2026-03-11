using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Defines an automation trigger — the event that starts an automation.
/// Triggers are discovered at startup and registered in the trigger catalogue.
/// </summary>
public interface ITrigger : IDiscoverable
{
    /// <summary>
    /// Gets the unique alias for this trigger (e.g. "contentPublished").
    /// </summary>
    string Alias { get; }

    /// <summary>
    /// Gets the human-readable display name (e.g. "Content Published").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets an optional description of what this trigger does.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Gets the category group for UI organisation (e.g. "Content", "Media").
    /// </summary>
    string? Group { get; }

    /// <summary>
    /// Gets the Umbraco icon alias (e.g. "icon-globe").
    /// </summary>
    string? Icon { get; }

    /// <summary>
    /// Gets the settings POCO type that drives the configuration UI, or null if the trigger has no settings.
    /// </summary>
    Type? SettingsType { get; }

    /// <summary>
    /// Gets the output POCO type that describes the data produced by this trigger, or null if no output.
    /// </summary>
    Type? OutputType { get; }

    /// <summary>
    /// Gets the settings schema used to render the configuration UI.
    /// </summary>
    EditableModelSchema? GetSettingsSchema();

    /// <summary>
    /// Gets the output properties available for expression binding.
    /// </summary>
    IReadOnlyList<TriggerOutputProperty> GetOutputProperties();

    /// <summary>
    /// Resolves trigger settings from a raw dictionary to a typed instance,
    /// applying configuration variable substitution and validation.
    /// </summary>
    /// <param name="settings">The raw settings dictionary from the trigger configuration.</param>
    /// <returns>The resolved settings object, or null if settings are empty or the trigger has no settings type.</returns>
    object? ResolveSettings(Dictionary<string, object?> settings);
}
