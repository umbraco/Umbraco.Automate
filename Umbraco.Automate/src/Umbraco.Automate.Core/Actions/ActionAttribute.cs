using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Actions;

/// <summary>
/// Marks a class as an automation action and provides discovery metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ActionAttribute : StepTypeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActionAttribute"/> class.
    /// </summary>
    /// <param name="alias">A unique, URL-safe alias for the action (e.g. "httpRequest").</param>
    /// <param name="name">A human-readable display name (e.g. "HTTP Request").</param>
    public ActionAttribute(string alias, string name) : base(alias, name) { }
}
