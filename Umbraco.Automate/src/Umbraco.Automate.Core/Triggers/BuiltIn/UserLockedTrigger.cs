using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a back-office user account is locked (typically after exceeding the
/// failed-login threshold).
/// </summary>
[Trigger("umbracoAutomate.userLocked", "User Locked",
    Description = "Fires when a back-office user account is locked.",
    Group = "Users",
    Icon = "icon-lock")]
public sealed class UserLockedTrigger
    : NotificationTriggerBase<UserAuthTriggerSettings, UserAuthTriggerOutput, UserLockedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserLockedTrigger"/> class.
    /// </summary>
    public UserLockedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UserLockedNotification notification)
        => UserAuthEventMapper.Map(Alias, notification);
}
