using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Defines a connection type — a reusable credential/integration pattern (e.g. "Slack", "SMTP", "OAuth2").
/// Connection types are discovered at startup and registered in the connection type catalogue.
/// Each <see cref="Connection"/> instance references a type by alias and stores type-specific settings.
/// </summary>
public interface IConnectionType : IDiscoverable
{
    /// <summary>
    /// Gets the unique alias for this connection type (e.g. "slack", "smtp").
    /// </summary>
    string Alias { get; }

    /// <summary>
    /// Gets the human-readable display name (e.g. "Slack", "SMTP").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets an optional description of what this connection type provides.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Gets the category group for UI organisation (e.g. "Messaging", "Email").
    /// </summary>
    string? Group { get; }

    /// <summary>
    /// Gets the Umbraco icon alias (e.g. "icon-message").
    /// </summary>
    string? Icon { get; }

    /// <summary>
    /// Gets the settings POCO type that drives the configuration UI, or null if the type has no settings.
    /// </summary>
    Type? SettingsType { get; }

    /// <summary>
    /// Gets the settings schema used to render the configuration UI.
    /// </summary>
    EditableModelSchema? GetSettingsSchema();

    /// <summary>
    /// Resolves connection settings from a raw dictionary to a typed instance,
    /// applying configuration variable substitution and validation.
    /// </summary>
    /// <param name="settings">The raw settings dictionary from the connection configuration.</param>
    /// <returns>The resolved settings object, or null if settings are empty or the type has no settings.</returns>
    object? ResolveSettings(Dictionary<string, object?> settings);

    /// <summary>
    /// Validates that the connection settings are correct and the external system is reachable.
    /// Override to implement connectivity checks (e.g. test API token, verify SMTP host).
    /// The default implementation returns <see cref="ConnectionValidationStatus.Warning"/> to
    /// signal that no concrete check was run — the UI surfaces this as "not verified".
    /// </summary>
    /// <param name="settings">The resolved settings object.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    Task<ConnectionValidationResult> ValidateAsync(object? settings, CancellationToken cancellationToken);
}
