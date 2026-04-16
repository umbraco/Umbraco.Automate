using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Custom <see cref="IBackOfficeSecurityAccessor"/> that supports both HTTP request contexts
/// (standard Umbraco backoffice) and background automation execution contexts via <see cref="AsyncLocal{T}"/>.
/// During automation execution, the <see cref="BackOfficeIdentityMiddleware"/> sets the
/// service principal identity so that any Umbraco CMS code that resolves the current user
/// sees the workspace's service account.
/// </summary>
internal sealed class AutomateBackOfficeSecurityAccessor : IBackOfficeSecurityAccessor
{
    private static readonly AsyncLocal<IBackOfficeSecurity?> AutomationSecurity = new();
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AutomateBackOfficeSecurityAccessor(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    /// <inheritdoc />
    /// <remarks>
    /// Returns the automation-scoped identity when running inside the action middleware pipeline,
    /// otherwise falls back to the standard HTTP-context-based resolution used by Umbraco's
    /// default <c>BackOfficeSecurityAccessor</c>.
    /// </remarks>
    public IBackOfficeSecurity? BackOfficeSecurity
        => AutomationSecurity.Value
           ?? _httpContextAccessor.HttpContext?.RequestServices?.GetService<IBackOfficeSecurity>();

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
