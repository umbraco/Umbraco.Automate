using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Cms.Core.Composing;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Defines an automation trigger — the event that starts an automation.
/// Triggers are discovered at startup and registered in the trigger catalogue.
/// </summary>
public interface ITrigger : IStepType
{
    /// <summary>
    /// Gets the output POCO type that describes the data produced by this trigger, or null if no output.
    /// </summary>
    Type? OutputType { get; }

    /// <summary>
    /// Gets the output properties available for expression binding.
    /// </summary>
    IReadOnlyList<TriggerOutputProperty> GetOutputProperties();
}
