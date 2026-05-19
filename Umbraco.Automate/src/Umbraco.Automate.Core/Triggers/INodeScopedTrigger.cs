namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// A trigger whose output is bound to a CMS content or media node. Enables the dispatcher
/// to authorise the event against the workspace service account's start node and granular
/// permissions, so a workspace with section access but a restricted start node does not
/// receive payloads for nodes outside its accessible path.
/// </summary>
public interface INodeScopedTrigger : ITrigger
{
    /// <summary>
    /// Returns the CMS node this event refers to, or <c>null</c> when the output is not
    /// bound to a single node (for example, a config-level event the trigger considers
    /// unrestricted by start-node).
    /// </summary>
    NodeScopedTriggerTarget? GetTargetNode(object output);
}
