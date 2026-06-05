namespace Umbraco.Automate.Core.Dispatch.Authorization;

/// <summary>
/// Marker for built-in trigger outputs whose event subject is a CMS content node.
/// Recognised by <see cref="NodeScopedTriggerDispatchAuthorizer"/>, which authorises the
/// workspace service account against the target node's start-node path and Browse
/// permission before dispatch.
/// </summary>
/// <remarks>
/// Internal to Umbraco.Automate.Core because it's the contract between Automate's own
/// content-scoped outputs and its built-in authoriser. Provider packages with their own
/// CMS-content-scoped outputs should declare their own marker (the recommended pattern —
/// see Umbraco.Workflow.Automate's <c>IContentScopedWorkflowOutput</c>).
/// </remarks>
internal interface IContentScopedTriggerOutput
{
    /// <summary>
    /// Returns the content key the event refers to, or <c>null</c> when the event is not
    /// bound to a single node — the authoriser treats <c>null</c> as "not applicable".
    /// </summary>
    Guid? GetContentKey();
}
