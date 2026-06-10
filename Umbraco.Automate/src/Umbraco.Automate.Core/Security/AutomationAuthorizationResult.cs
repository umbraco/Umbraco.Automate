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
}
