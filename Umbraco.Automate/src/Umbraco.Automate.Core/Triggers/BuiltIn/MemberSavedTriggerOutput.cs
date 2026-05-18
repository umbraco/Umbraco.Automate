namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="MemberSavedTrigger"/> for each saved member.
/// </summary>
public sealed class MemberSavedTriggerOutput
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

    /// <summary>
    /// Gets a value indicating whether this save represents a newly-created member.
    /// True when <c>CreateDate == UpdateDate</c> on the saved entity. Soft signal —
    /// database date precision may vary, so downstream automations needing a hard
    /// guarantee should re-fetch.
    /// </summary>
    public bool IsNew { get; init; }
}
