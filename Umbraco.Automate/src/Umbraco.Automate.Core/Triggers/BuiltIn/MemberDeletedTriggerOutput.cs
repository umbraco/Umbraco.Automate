namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="MemberDeletedTrigger"/> for each deleted member.
/// </summary>
public sealed class MemberDeletedTriggerOutput
{
    /// <summary>
    /// Gets the member's unique key.
    /// </summary>
    public Guid MemberKey { get; init; }

    /// <summary>
    /// Gets the member's display name.
    /// </summary>
    public string? MemberName { get; init; }

    /// <summary>
    /// Gets the member's username.
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Gets the member's email address.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Gets the member type's unique key.
    /// </summary>
    public Guid? MemberTypeKey { get; init; }

    /// <summary>
    /// Gets the member type alias.
    /// </summary>
    public string? MemberTypeAlias { get; init; }
}
