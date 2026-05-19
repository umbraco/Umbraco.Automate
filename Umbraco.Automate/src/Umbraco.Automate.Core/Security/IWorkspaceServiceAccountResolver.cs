using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Resolves the Umbraco <see cref="IUser"/> service account configured on a workspace.
/// Returns <c>null</c> when the workspace does not exist, has no service account assigned,
/// or the configured user has been deleted — callers treat any of these as a deny.
/// </summary>
public interface IWorkspaceServiceAccountResolver
{
    /// <summary>
    /// Resolves the service account user for the given workspace.
    /// </summary>
    Task<IUser?> GetServiceAccountAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
