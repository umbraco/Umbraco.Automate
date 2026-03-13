using System.Reflection;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Notifications.Channels;

/// <summary>
/// Base class for notification channels that reads metadata from <see cref="NotificationChannelAttribute"/>
/// and auto-derives settings schema.
/// </summary>
/// <typeparam name="TSettings">The settings POCO type (use <see cref="object"/> if no settings).</typeparam>
public abstract class NotificationChannelBase<TSettings> : INotificationChannel
    where TSettings : class, new()
{
    private readonly NotificationChannelAttribute _attribute;
    private readonly NotificationChannelInfrastructure _infrastructure;

    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationChannelBase{TSettings}"/> class.
    /// </summary>
    protected NotificationChannelBase(NotificationChannelInfrastructure infrastructure)
    {
        _infrastructure = infrastructure;
        _attribute = GetType().GetCustomAttribute<NotificationChannelAttribute>(inherit: false)
            ?? throw new InvalidOperationException(
                $"NotificationChannel '{GetType().FullName}' is missing required [NotificationChannel] attribute.");
    }

    /// <inheritdoc />
    public string Alias => _attribute.Alias;

    /// <inheritdoc />
    public string Name => _attribute.Name;

    /// <inheritdoc />
    public abstract string? Description { get; }

    /// <inheritdoc />
    public abstract string? Icon { get; }

    /// <inheritdoc />
    public Type? SettingsType => typeof(TSettings) == typeof(object) ? null : typeof(TSettings);

    /// <inheritdoc />
    public EditableModelSchema? GetSettingsSchema()
    {
        if (SettingsType is null)
        {
            return null;
        }

        return EditableModelSchemaBuilder.Build(SettingsType);
    }

    /// <summary>
    /// Resolves settings from a raw dictionary to a typed instance.
    /// </summary>
    public TSettings? ResolveSettings(Dictionary<string, object?> settings)
        => _infrastructure.ModelResolver.ResolveModel<TSettings>(Alias, settings, GetSettingsSchema());

    /// <inheritdoc />
    object? INotificationChannel.ResolveSettings(Dictionary<string, object?> settings)
        => ResolveSettings(settings);

    /// <inheritdoc />
    public Task NotifyAsync(RunNotification notification, object? settings, CancellationToken cancellationToken)
        => NotifyAsync(notification, settings as TSettings ?? new TSettings(), cancellationToken);

    /// <summary>
    /// Sends a notification through this channel with typed settings.
    /// </summary>
    protected abstract Task NotifyAsync(RunNotification notification, TSettings settings, CancellationToken cancellationToken);
}
