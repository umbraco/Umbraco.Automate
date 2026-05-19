using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

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
    private readonly IUserService _userService;
    private readonly ILogger<UserLockedTrigger> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserLockedTrigger"/> class.
    /// </summary>
    public UserLockedTrigger(
        TriggerInfrastructure infrastructure,
        IUserService userService,
        ILogger<UserLockedTrigger> logger)
        : base(infrastructure)
    {
        _userService = userService;
        _logger = logger;
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(UserLockedNotification notification)
        => UserAuthEventMapper.Map(Alias, notification,
            UserGroupResolver.Resolve(_userService, notification.AffectedUserId, _logger));

    /// <inheritdoc />
    protected override bool CanHandle(UserAuthTriggerOutput output, UserAuthTriggerSettings? settings)
        => GroupKeysFilter.Matches(output.UserGroupKeys, settings?.UserGroups);
}
