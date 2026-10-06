namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Result of authorising a service account against a content or media node.
/// </summary>
public readonly record struct AutomationAuthorizationResult(bool Authorized, string? FailureReason)
{
    /// <summary>
    /// A successful authorisation result.
    /// </summary>
    public static AutomationAuthorizationResult Success { get; } = new(true, null);

    /// <summary>
    /// Builds a failure result with the given reason.
    /// </summary>
    public static AutomationAuthorizationResult Fail(string reason) => new(false, reason);

    /// <summary>
    /// <c>true</c> when the CMS reported the node as not existing, as opposed to the account
    /// being denied access to a node that does exist. Always accompanies
    /// <see cref="Authorized"/> being <c>false</c> and a populated <see cref="FailureReason"/>,
    /// so callers that only check <see cref="Authorized"/> behave as they always have.
    /// </summary>
    /// <remarks>
    /// Set when the node does not exist, and also when it sits in the recycle bin and the
    /// account has no root start node: such an account cannot see the bin, so a trashed node
    /// looks the same to it as a deleted one. A live node the account is denied stays a
    /// plain failure, so this flag cannot reveal a node the account is not allowed to see.
    /// </remarks>
    public bool IsNotFound { get; init; }

    /// <summary>
    /// Builds a failure result for a node the CMS reported as not existing.
    /// </summary>
    public static AutomationAuthorizationResult NotFound(string reason) => new(false, reason) { IsNotFound = true };
}
