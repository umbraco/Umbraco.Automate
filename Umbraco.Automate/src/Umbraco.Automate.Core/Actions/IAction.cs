using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Defines an automation action — a reusable unit of work within an automation.
/// Actions are discovered at startup and registered in the action catalogue.
/// </summary>
public interface IAction : IStepType
{
    /// <summary>
    /// Executes the action.
    /// </summary>
    /// <param name="context">The execution context containing settings, inputs, and run metadata.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the action execution.</returns>
    Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken);
}
