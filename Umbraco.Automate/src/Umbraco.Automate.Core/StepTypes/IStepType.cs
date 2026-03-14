using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.StepTypes;

/// <summary>
/// Base interface for all step types in the automation catalogue (actions, control flow, etc.).
/// Provides shared metadata and settings resolution used for discovery and configuration UI.
/// </summary>
public interface IStepType : IDiscoverable
{
    /// <summary>
    /// Gets the unique alias for this step type (e.g. "httpRequest", "umbracoAutomate.if").
    /// </summary>
    string Alias { get; }

    /// <summary>
    /// Gets the human-readable display name (e.g. "HTTP Request", "If").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets an optional description of what this step type does.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Gets the category group for UI organisation (e.g. "Core", "Flow Control").
    /// </summary>
    string? Group { get; }

    /// <summary>
    /// Gets the Umbraco icon alias (e.g. "icon-message").
    /// </summary>
    string? Icon { get; }

    /// <summary>
    /// Gets the settings POCO type that drives the configuration UI, or null if the step type has no settings.
    /// </summary>
    Type? SettingsType { get; }

    /// <summary>
    /// Gets the settings schema used to render the configuration UI.
    /// </summary>
    EditableModelSchema? GetSettingsSchema();

    /// <summary>
    /// Resolves step type settings from a raw dictionary to a typed instance,
    /// applying configuration variable substitution and validation.
    /// </summary>
    /// <param name="settings">The raw settings dictionary from the step configuration.</param>
    /// <returns>The resolved settings object, or null if settings are empty or the step type has no settings type.</returns>
    object? ResolveSettings(Dictionary<string, object?> settings);
}
