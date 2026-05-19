using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Fires when a back-office user successfully logs in.
/// </summary>
[Trigger("umbracoAutomate.userLoginSuccess", "User Login Success",
    Description = "Fires when a back-office user successfully logs in.",
    Group = "Users",
    Icon = "icon-unlocked")]
public sealed class UserLoginSuccessTrigger
    : NotificationTriggerBase<UserAuthTriggerSettings, UserAuthTriggerOutput, UserLoginSuccessNotification>
{
    private readonly IUserService _userService;
    private readonly ILogger<UserLoginSuccessTrigger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserLoginSuccessTrigger"/> class.
    /// </summary>
    public UserLoginSuccessTrigger(
        TriggerInfrastructure infrastructure,
        IUserService userService,
        ILogger<UserLoginSuccessTrigger> logger)
        : base(infrastructure)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UserLoginSuccessNotification notification)
        => UserAuthEventMapper.Map(Alias, notification,
            UserGroupResolver.Resolve(_userService, notification.AffectedUserId, _logger));

    /// <inheritdoc />
    protected override bool CanHandle(UserAuthTriggerOutput output, UserAuthTriggerSettings? settings)
        => GroupKeysFilter.Matches(output.UserGroupKeys, settings?.UserGroups);
}
