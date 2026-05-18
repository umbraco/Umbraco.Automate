namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="UserDeletedTrigger"/> for each deleted user.
/// </summary>
public sealed class UserDeletedTriggerOutput
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
}
