using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Notifications.Channels;

/// <summary>
/// Defines a notification channel that can deliver alerts when automation runs complete.
/// Channels are discovered at startup via <see cref="NotificationChannelAttribute"/>.
/// </summary>
public interface INotificationChannel : IDiscoverable
{
    /// <summary>
    /// Gets the unique alias for this channel.
    /// </summary>
    string Alias { get; }

    /// <summary>
    /// Gets the human-readable display name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets an optional description.
    /// </summary>
    string? Description { get; }

    /// <summary>
    /// Gets the Umbraco icon alias.
    /// </summary>
    string? Icon { get; }

    /// <summary>
    /// Gets the settings POCO type that drives the configuration UI, or null if no settings.
    /// </summary>
    Type? SettingsType { get; }

    /// <summary>
    /// Gets the settings schema used to render the configuration UI.
    /// </summary>
    EditableModelSchema? GetSettingsSchema();

    /// <summary>
    /// Resolves channel settings from a raw dictionary to a typed instance.
    /// </summary>
    object? ResolveSettings(Dictionary<string, object?> settings);

    /// <summary>
    /// Sends a notification through this channel.
    /// </summary>
    Task NotifyAsync(RunNotification notification, object? settings, CancellationToken cancellationToken);
}
