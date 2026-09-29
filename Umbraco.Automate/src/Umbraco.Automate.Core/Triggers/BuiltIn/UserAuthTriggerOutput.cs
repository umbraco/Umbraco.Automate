using Json.Schema.Generation;

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
    [Description("The affected user id (the user the event is about). May be null on a failed login.")]
    public string? AffectedUserId { get; init; }

    /// <summary>
    /// Gets the performing user id (the user who initiated the action — often the
    /// same as <see cref="AffectedUserId"/> for self-actions). Set to <c>"-1"</c>
    /// by Umbraco when no back-office user is the actor.
    /// </summary>
    [Description("The performing user id (the user who initiated the action). \"-1\" when no back-office user is the actor.")]
    public string? PerformingUserId { get; init; }

    /// <summary>
    /// Gets the source IP address of the request.
    /// </summary>
    [Description("The source IP address of the request.")]
    public string? IpAddress { get; init; }

    /// <summary>
    /// Gets the UTC timestamp at which the event was raised by the CMS.
    /// </summary>
    [Description("The UTC timestamp at which the event was raised by the CMS.")]
    public DateTime DateTimeUtc { get; init; }

    /// <summary>
    /// Gets the keys of the user groups the affected user belongs to. Resolved at
    /// notification time by looking up the user via <c>IUserService</c>; empty when the
    /// id is missing, unparseable (legacy int identities), or the user can't be found.
    /// </summary>
    [Description("The keys of the user groups the affected user belongs to.")]
    public IReadOnlyList<Guid> UserGroupKeys { get; init; } = Array.Empty<Guid>();
}
