using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a back-office user's password is changed.
/// </summary>
[Trigger("umbracoAutomate.userPasswordChanged", "User Password Changed",
    Description = "Fires when a back-office user's password is changed.",
    Group = "Users",
    Icon = "icon-key")]
public sealed class UserPasswordChangedTrigger
    : NotificationTriggerBase<UserAuthTriggerSettings, UserAuthTriggerOutput, UserPasswordChangedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserPasswordChangedTrigger"/> class.
    /// </summary>
    public UserPasswordChangedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UserPasswordChangedNotification notification)
        => UserAuthEventMapper.Map(Alias, notification);
}
