using Json.Schema.Generation;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="MemberDeletedTrigger"/> for each deleted member.
/// </summary>
public sealed class MemberDeletedTriggerOutput
{
    /// <summary>
    /// Gets the member's unique key.
    /// </summary>
    [Description("The member's unique key.")]
    public Guid MemberKey { get; init; }

    /// <summary>
    /// Gets the member's display name.
    /// </summary>
    [Description("The member's display name.")]
    public string? MemberName { get; init; }

    /// <summary>
    /// Gets the member's username.
    /// </summary>
    [Description("The member's username.")]
    public string? Username { get; init; }

    /// <summary>
    /// Gets the member's email address.
    /// </summary>
    [Description("The member's email address.")]
    public string? Email { get; init; }

    /// <summary>
    /// Gets the member type's unique key.
    /// </summary>
    [Description("The member type's unique key.")]
    public Guid? MemberTypeKey { get; init; }

    /// <summary>
    /// Gets the member type alias.
    /// </summary>
    [Description("The member type alias.")]
    public string? MemberTypeAlias { get; init; }

    /// <summary>
    /// Gets the keys of the member groups this member belonged to at delete time.
    /// </summary>
    [Description("The keys of the member groups this member belonged to at delete time.")]
    public IReadOnlyList<Guid> MemberGroupKeys { get; init; } = Array.Empty<Guid>();
}
