using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.ControlFlow;

/// <summary>
/// Defines a control flow step type — a step that controls execution flow rather than performing work.
/// Control flow types are compiled into WorkflowCore step bodies by the compiler, not executed through
/// the action middleware pipeline.
/// </summary>
public interface IControlFlow : IStepType;
