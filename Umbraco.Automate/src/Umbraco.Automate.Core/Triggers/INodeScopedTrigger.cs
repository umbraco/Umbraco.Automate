namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Marker for built-in CMS content / media triggers. Recognised by the built-in
/// <c>NodeScopedTriggerDispatchAuthorizer</c>, which authorises the workspace service
/// account against the target node's start-node path and Browse permission.
/// </summary>
/// <remarks>
/// Internal because the public extensibility point for dispatch-time authorisation is
/// <c>ITriggerDispatchAuthorizer</c>. Provider packages with non-CMS resource models
/// (e.g. Umbraco Commerce stores) register their own authoriser rather than reuse this
/// marker.
/// </remarks>
internal interface INodeScopedTrigger : ITrigger
{
    /// <summary>
    /// Returns the CMS node this event refers to, or <c>null</c> when the output is not
    /// bound to a single node (for example, a config-level event the trigger considers
    /// unrestricted by start-node).
    /// </summary>
    NodeScopedTriggerTarget? GetTargetNode(object output);
}
