namespace Umbraco.Automate.Core.Dispatch.Authorization;

/// <summary>
/// Marker for built-in trigger outputs whose event subject is a CMS Library element. Routed
/// through <see cref="Security.IAutomationActionAuthorizer.AuthorizeElementAsync(Umbraco.Cms.Core.Models.Membership.IUser, Guid, IReadOnlySet{string}, CancellationToken)"/>,
/// which checks Read on the element (folder permissions and Library start node included).
/// </summary>
/// <remarks>Same internal-only convention as <see cref="IContentScopedTriggerOutput"/>.</remarks>
internal interface IElementScopedTriggerOutput
{
    /// <summary>
    /// Returns the element key the event refers to, or <c>null</c> when the event is not
    /// bound to a single element — the authoriser treats <c>null</c> as "not applicable".
    /// </summary>
    Guid? GetElementKey();
}
