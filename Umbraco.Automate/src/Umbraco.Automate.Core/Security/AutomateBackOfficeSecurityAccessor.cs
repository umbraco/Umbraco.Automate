using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Decorator around Umbraco's default <see cref="IBackOfficeSecurityAccessor"/> that adds
/// <see cref="AsyncLocal{T}"/>-based identity support for background automation contexts.
/// When the <see cref="BackOfficeIdentityMiddleware"/> sets the service principal during an
/// automation run, this accessor returns it. Outside of automation runs, it delegates to the
/// original Umbraco accessor unchanged.
/// </summary>
internal sealed class AutomateBackOfficeSecurityAccessor : IBackOfficeSecurityAccessor
{
    private static readonly AsyncLocal<IBackOfficeSecurity?> AutomationSecurity = new();
    private readonly IBackOfficeSecurityAccessor _inner;

    public AutomateBackOfficeSecurityAccessor(IBackOfficeSecurityAccessor inner)
        => _inner = inner;

    /// <inheritdoc />
    /// <remarks>
    /// Returns the automation-scoped identity when running inside the action middleware pipeline,
    /// otherwise delegates to the original Umbraco accessor (HTTP-context-based resolution).
    /// </remarks>
    public IBackOfficeSecurity? BackOfficeSecurity
        => AutomationSecurity.Value ?? _inner.BackOfficeSecurity;

    /// <summary>
    /// Sets the backoffice identity for the current async flow. Returns an <see cref="IDisposable"/>
    /// that clears the identity when disposed.
    /// </summary>
    internal static IDisposable Set(IBackOfficeSecurity security)
    {
        AutomationSecurity.Value = security;
        return new SecurityScope();
    }

    /// <summary>
    /// Returns the current <see cref="AsyncLocal{T}"/>-scoped identity, for test assertions.
    /// </summary>
    internal static IBackOfficeSecurity? GetCurrentForTesting() => AutomationSecurity.Value;

    private sealed class SecurityScope : IDisposable
    {
        public void Dispose() => AutomationSecurity.Value = null;
    }
}
