using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Core.Dispatch.Authorization;

/// <summary>
/// A dispatch authoriser that can let a run go ahead with a <em>reduced</em> trigger output
/// rather than only allow or deny it whole — for example a batch trigger where the service
/// account may see some items but not others. <see cref="TriggerEventHandler"/> prefers
/// <see cref="AuthorizeAndNarrowAsync"/> over <see cref="ITriggerDispatchAuthorizer.AuthorizeAsync"/>
/// when an authoriser implements this, hands the narrowed output to the authorisers after
/// it, and starts the run with the narrowed output.
/// </summary>
/// <remarks>
/// <para>
/// The result must depend only on <see cref="TriggerDispatchAuthorizationContext.ServiceAccount"/>
/// and <see cref="TriggerDispatchAuthorizationContext.TypedOutput"/> — never on the
/// automation or trigger. <see cref="TriggerEventHandler"/> relies on this to reuse one
/// result for every automation of an event that shares a service account.
/// </para>
/// <para>
/// Internal for now: only the built-in <see cref="NodeScopedTriggerDispatchAuthorizer"/>
/// narrows. Making it public later is additive.
/// </para>
/// </remarks>
internal interface IOutputNarrowingTriggerDispatchAuthorizer : ITriggerDispatchAuthorizer
{
    /// <summary>
    /// Authorises dispatch and, when only part of the output may be shown to the service
    /// account, returns the reduced output to run with.
    /// </summary>
    Task<TriggerDispatchNarrowingResult> AuthorizeAndNarrowAsync(
        TriggerDispatchAuthorizationContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Outcome of <see cref="IOutputNarrowingTriggerDispatchAuthorizer.AuthorizeAndNarrowAsync"/>.
/// </summary>
/// <param name="Result">Whether dispatch may proceed. A failure skips the run.</param>
/// <param name="NarrowedOutput">
/// The reduced output to run with, or <c>null</c> when the output is unchanged. Always
/// <c>null</c> when <paramref name="Result"/> is a failure.
/// </param>
internal readonly record struct TriggerDispatchNarrowingResult(
    AutomationAuthorizationResult Result,
    object? NarrowedOutput)
{
    /// <summary>Dispatch may proceed with the output unchanged.</summary>
    public static TriggerDispatchNarrowingResult Unchanged { get; } = new(AutomationAuthorizationResult.Success, null);

    /// <summary>Dispatch may proceed with <paramref name="output"/> in place of the original.</summary>
    public static TriggerDispatchNarrowingResult Narrowed(object output) => new(AutomationAuthorizationResult.Success, output);

    /// <summary>Dispatch is denied.</summary>
    public static TriggerDispatchNarrowingResult Denied(AutomationAuthorizationResult failure) => new(failure, null);
}
