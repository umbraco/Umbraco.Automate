using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Marks a class as an automation trigger and provides discovery metadata.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class TriggerAttribute : StepTypeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TriggerAttribute"/> class.
    /// </summary>
    /// <param name="alias">A unique, URL-safe alias for the trigger (e.g. "contentPublished").</param>
    /// <param name="name">A human-readable display name (e.g. "Content Published").</param>
    public TriggerAttribute(string alias, string name) : base(alias, name) { }
}
