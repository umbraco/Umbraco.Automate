using Umbraco.Cms.Core.Notifications;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a back-office user successfully logs in.
/// </summary>
[Trigger("umbracoAutomate.userLoginSuccess", "User Login Success",
    Description = "Fires when a back-office user successfully logs in.",
    Group = "Users",
    Icon = "icon-lock-open")]
public sealed class UserLoginSuccessTrigger
    : NotificationTriggerBase<UserAuthTriggerSettings, UserAuthTriggerOutput, UserLoginSuccessNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserLoginSuccessTrigger"/> class.
    /// </summary>
    public UserLoginSuccessTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UserLoginSuccessNotification notification)
        => UserAuthEventMapper.Map(Alias, notification);
}
