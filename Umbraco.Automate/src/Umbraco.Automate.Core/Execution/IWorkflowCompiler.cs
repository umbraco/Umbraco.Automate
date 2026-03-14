using Umbraco.Automate.Core.Automations;
using WorkflowCore.Models;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Compiles an automation definition into a WorkflowCore <see cref="WorkflowDefinition"/>.
/// Resolves step aliases against action and control flow collections, creates step bodies,
/// and wires transitions.
/// </summary>
internal interface IWorkflowCompiler
{
    /// <summary>
    /// Compiles the given automation into a WorkflowCore workflow definition.
    /// </summary>
    /// <param name="automation">The automation to compile.</param>
    /// <param name="workflowId">The unique workflow definition ID.</param>
    /// <returns>The compiled workflow definition.</returns>
    WorkflowDefinition Compile(Automation automation, string workflowId);
}
