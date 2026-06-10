using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.ControlFlow;

/// <summary>
/// Marks a class as a control flow step type and provides discovery metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ControlFlowAttribute : StepTypeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ControlFlowAttribute"/> class.
    /// </summary>
    /// <param name="alias">A unique, URL-safe alias for the control flow step type.</param>
    /// <param name="name">A human-readable display name.</param>
    public ControlFlowAttribute(string alias, string name) : base(alias, name) { }
}
