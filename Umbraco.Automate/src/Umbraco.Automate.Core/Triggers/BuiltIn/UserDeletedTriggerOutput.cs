using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="UserDeletedTrigger"/> for each deleted user.
/// </summary>
public sealed class UserDeletedTriggerOutput
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
    /// Gets the keys of the user groups this user belonged to at delete time.
    /// </summary>
    [Description("The keys of the user groups this user belonged to at delete time.")]
    public IReadOnlyList<Guid> UserGroupKeys { get; init; } = Array.Empty<Guid>();
}
