namespace Umbraco.Automate.Core.Dispatch.Authorization;

/// <summary>
/// Marker for built-in trigger outputs whose event subject is a CMS media node. Routed
/// through <c>IMediaPermissionService</c>; media has no per-verb permissions so access is
/// binary (section + start node).
/// </summary>
/// <remarks>Same internal-only convention as <see cref="IContentScopedTriggerOutput"/>.</remarks>
internal interface IMediaScopedTriggerOutput
{
    /// <summary>
    /// Returns the media key the event refers to, or <c>null</c> when the event is not
    /// bound to a single node — the authoriser treats <c>null</c> as "not applicable".
    /// </summary>
    Guid? GetMediaKey();
}
