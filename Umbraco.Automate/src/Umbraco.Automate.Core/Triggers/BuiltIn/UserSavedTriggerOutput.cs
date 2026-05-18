namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="UserSavedTrigger"/> for each saved user.
/// </summary>
public sealed class UserSavedTriggerOutput
{
    /// <summary>
    /// Gets the user's unique key.
    /// </summary>
    public Guid UserKey { get; init; }

    /// <summary>
    /// Gets the user's display name.
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Gets the user's username.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets the user's email address.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Gets a value indicating whether this save represents a newly-created user.
    /// True when <c>CreateDate == UpdateDate</c> on the saved entity. Soft signal —
    /// database date precision may vary, so downstream automations needing a hard
    /// guarantee should re-fetch.
    /// </summary>
    public bool IsNew { get; init; }
}
