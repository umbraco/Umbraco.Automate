using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Defines an automation action — a reusable unit of work within an automation.
/// Actions are discovered at startup and registered in the action catalogue.
/// </summary>
public interface IAction : IDiscoverable
{
    /// <summary>
    /// Gets the unique alias for this action (e.g. "httpRequest").
    /// </summary>
    string Alias { get; }

    /// <summary>
    /// Gets the human-readable display name (e.g. "HTTP Request").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets an optional description of what this action does.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Gets the category group for UI organisation (e.g. "Core", "Content").
    /// </summary>
    string? Group { get; }

    /// <summary>
    /// Gets the Umbraco icon alias (e.g. "icon-message").
    /// </summary>
    string? Icon { get; }

    /// <summary>
    /// Gets the settings POCO type that drives the configuration UI, or null if the action has no settings.
    /// </summary>
    Type? SettingsType { get; }

    /// <summary>
    /// Gets the settings schema used to render the configuration UI.
    /// </summary>
    EditableModelSchema? GetSettingsSchema();

    /// <summary>
    /// Resolves action settings from a raw dictionary to a typed instance,
    /// applying configuration variable substitution and validation.
    /// </summary>
    /// <param name="settings">The raw settings dictionary from the step configuration.</param>
    /// <returns>The resolved settings object, or null if settings are empty or the action has no settings type.</returns>
    object? ResolveSettings(Dictionary<string, object?> settings);

    /// <summary>
    /// Executes the action.
    /// </summary>
    /// <param name="context">The execution context containing settings, inputs, and run metadata.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the action execution.</returns>
    Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken);
}
