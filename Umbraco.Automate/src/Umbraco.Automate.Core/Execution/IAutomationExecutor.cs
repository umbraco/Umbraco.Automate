using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Executes an automation by compiling it to a WorkflowCore workflow and starting a run.
/// </summary>
public interface IAutomationExecutor
{
    /// <summary>
    /// Executes the given automation, creating a run and starting the WorkflowCore workflow.
    /// </summary>
    /// <param name="automation">The automation to execute.</param>
    /// <param name="initiatorType">The type of initiator ("system", "user", "webhook", "ai-agent").</param>
    /// <param name="initiatorId">An optional identifier for the initiator.</param>
    /// <param name="triggerOutputData">The trigger output data, if any.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <param name="originChain">
    /// Inherited upstream automation chain (oldest to newest). <c>null</c> or empty for
    /// runs started by a non-automation source; otherwise the chain carried by the trigger
    /// event that caused this run. Does NOT include the current automation.
    /// </param>
    /// <returns>The ID of the created run.</returns>
    Task<Guid> ExecuteAsync(
        Automation automation,
        string initiatorType,
        string? initiatorId,
        Dictionary<string, object?>? triggerOutputData,
        CancellationToken cancellationToken,
        IReadOnlyList<Guid>? originChain = null);
}
