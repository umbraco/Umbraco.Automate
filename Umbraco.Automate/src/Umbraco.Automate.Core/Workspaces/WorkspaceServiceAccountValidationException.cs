namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// Thrown by <see cref="IWorkspaceService"/> when a workspace's service account is not an API user,
/// so the workspace must not be saved. The message is shown to the backoffice user.
/// </summary>
internal sealed class WorkspaceServiceAccountValidationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceServiceAccountValidationException"/> class.
    /// </summary>
    public WorkspaceServiceAccountValidationException(string message)
        : base(message)
    {
    }
}
