using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Core.Dispatch.Authorization;

/// <summary>
/// Authorises a trigger event for a specific automation at dispatch time. Implementations
/// are iterated by <see cref="TriggerEventHandler"/> after the section guard passes, in
/// registration order. The first <see cref="AutomationAuthorizationResult.Authorized"/>
/// = <c>false</c> result short-circuits dispatch for that automation; no run is created.
/// </summary>
/// <remarks>
/// <para>
/// A built-in authoriser handles CMS content/media triggers
/// (see <c>NodeScopedTriggerDispatchAuthorizer</c>). Provider packages register additional
/// authorisers to gate against their own resource models — for example Umbraco Commerce's
/// per-store user/role allowlists, which can't be modelled as CMS start-node paths.
/// </para>
/// <para>
/// An authoriser that doesn't apply to the given trigger or output should return
/// <see cref="AutomationAuthorizationResult.Success"/> — the dispatcher treats Success as
/// "not blocking", not "explicitly approved".
/// </para>
/// </remarks>
public interface ITriggerDispatchAuthorizer
{
    /// <summary>
    /// Authorises dispatch of a trigger event for a specific automation. Returning
    /// <see cref="AutomationAuthorizationResult.Fail"/> skips the run for this automation;
    /// other authorisers in the collection are not consulted for this automation once one
    /// has denied.
    /// </summary>
    Task<AutomationAuthorizationResult> AuthorizeAsync(
        TriggerDispatchAuthorizationContext context,
        CancellationToken cancellationToken);
}
