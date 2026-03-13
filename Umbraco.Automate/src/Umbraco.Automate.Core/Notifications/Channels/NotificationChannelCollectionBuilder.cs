using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Notifications.Channels;

/// <summary>
/// Builder for the notification channel collection.
/// </summary>
public class NotificationChannelCollectionBuilder
    : LazyCollectionBuilderBase<NotificationChannelCollectionBuilder, NotificationChannelCollection, INotificationChannel>
{
    /// <inheritdoc />
    protected override NotificationChannelCollectionBuilder This => this;
}

/// <summary>
/// The collection of all registered notification channels.
/// </summary>
public sealed class NotificationChannelCollection : BuilderCollectionBase<INotificationChannel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationChannelCollection"/> class.
    /// </summary>
    public NotificationChannelCollection(Func<IEnumerable<INotificationChannel>> items) : base(items) { }

    /// <summary>
    /// Gets a channel by its alias.
    /// </summary>
    public INotificationChannel? GetByAlias(string alias)
        => this.FirstOrDefault(c => string.Equals(c.Alias, alias, StringComparison.OrdinalIgnoreCase));
}
