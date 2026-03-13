namespace Umbraco.Automate.Core.Notifications.Channels;

/// <summary>
/// Marks a class as a notification channel and provides discovery metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class NotificationChannelAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NotificationChannelAttribute"/> class.
    /// </summary>
    /// <param name="alias">A unique, URL-safe alias for the channel.</param>
    /// <param name="name">A human-readable display name.</param>
    public NotificationChannelAttribute(string alias, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        Alias = alias;
        Name = name;
    }

    /// <summary>
    /// Gets the unique alias.
    /// </summary>
    public string Alias { get; }

    /// <summary>
    /// Gets the display name.
    /// </summary>
    public string Name { get; }
}
