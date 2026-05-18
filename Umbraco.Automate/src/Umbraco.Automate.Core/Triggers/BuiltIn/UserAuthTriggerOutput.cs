namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output shared across the user auth triggers — mirrors the data carried by
/// <see cref="Umbraco.Cms.Core.Notifications.UserNotification"/>.
/// </summary>
public sealed class UserAuthTriggerOutput
{
    /// <summary>
    /// Gets the affected user id (the user the event is about). May be null on a
    /// failed login when the supplied username does not resolve to a known user.
    /// </summary>
    public string? AffectedUserId { get; init; }

    /// <summary>
    /// Gets the performing user id (the user who initiated the action — often the
    /// same as <see cref="AffectedUserId"/> for self-actions). Set to <c>"-1"</c>
    /// by Umbraco when no back-office user is the actor.
    /// </summary>
    public string? PerformingUserId { get; init; }

    /// <summary>
    /// Gets the source IP address of the request.
    /// </summary>
    public string? IpAddress { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which the event was raised by the CMS.
    /// </summary>
    public DateTime DateTimeUtc { get; init; }
}
