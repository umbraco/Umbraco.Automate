using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Notifications.Channels;

/// <summary>
/// Infrastructure services for notification channels.
/// </summary>
public sealed class NotificationChannelInfrastructure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationChannelInfrastructure"/> class.
    /// </summary>
    public NotificationChannelInfrastructure(IEditableModelResolver modelResolver)
    {
        ModelResolver = modelResolver;
    }

    /// <summary>
    /// Gets the model resolver for deserialising channel settings.
    /// </summary>
    internal IEditableModelResolver ModelResolver { get; }
}
