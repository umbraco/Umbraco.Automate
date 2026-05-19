namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Identifies the CMS node a node-scoped trigger event refers to. The dispatcher uses
/// this to authorise the workspace service account against the node before starting a run.
/// </summary>
public readonly record struct NodeScopedTriggerTarget(Guid Key, NodeScopedTriggerTargetKind Kind);

/// <summary>
/// The kind of CMS resource a <see cref="NodeScopedTriggerTarget"/> refers to. Selects which
/// Umbraco permission service the dispatcher consults.
/// </summary>
public enum NodeScopedTriggerTargetKind
{
    /// <summary>The target is a content node — authorised via <c>IContentPermissionService</c>.</summary>
    Content,

    /// <summary>The target is a media node — authorised via <c>IMediaPermissionService</c>.</summary>
    Media,
}
