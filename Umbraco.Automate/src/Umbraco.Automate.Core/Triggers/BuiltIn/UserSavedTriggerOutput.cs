using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="UserSavedTrigger"/> for each saved user.
/// </summary>
public sealed class UserSavedTriggerOutput
{
    /// <summary>
    /// Gets the user's unique key.
    /// </summary>
    [Description("The user's unique key.")]
    public Guid UserKey { get; init; }

    /// <summary>
    /// Gets the user's display name.
    /// </summary>
    [Description("The user's display name.")]
    public string? UserName { get; init; }

    /// <summary>
    /// Gets the user's username.
    /// </summary>
    [Description("The user's username.")]
    public string? Username { get; init; }

    /// <summary>
    /// Gets the user's email address.
    /// </summary>
    [Description("The user's email address.")]
    public string? Email { get; init; }

    /// <summary>
    /// Gets the keys of the user groups this user belongs to. Read directly from
    /// <c>IUser.Groups</c> at notification time — no extra service lookup needed.
    /// </summary>
    [Description("The keys of the user groups this user belongs to.")]
    public IReadOnlyList<Guid> UserGroupKeys { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// Gets a value indicating whether this save represents a newly-created user.
    /// True when <c>CreateDate == UpdateDate</c> on the saved entity. Soft signal —
    /// database date precision may vary, so downstream automations needing a hard
    /// guarantee should re-fetch.
    /// </summary>
    [Description("Whether this save represents a newly-created user.")]
    public bool IsNew { get; init; }
}
