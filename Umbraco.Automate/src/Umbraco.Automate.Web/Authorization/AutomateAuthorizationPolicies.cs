namespace Umbraco.Automate.Web.Authorization;

/// <summary>
/// Provides constants for Automate-related authorization policies.
/// </summary>
public static class AutomateAuthorizationPolicies
{
    /// <summary>
    /// Represents the policy name for accessing the Automate section.
    /// </summary>
    public const string SectionAccessAutomate = nameof(SectionAccessAutomate);

    /// <summary>
    /// Represents the policy name for workspace membership access (resource-based).
    /// </summary>
    public const string WorkspaceAccess = nameof(WorkspaceAccess);
}
