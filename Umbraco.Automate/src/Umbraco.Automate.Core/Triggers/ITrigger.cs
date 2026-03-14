using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Triggers;

/// <summary>
/// Defines an automation trigger — the event that starts an automation.
/// Triggers are discovered at startup and registered in the trigger catalogue.
/// </summary>
public interface ITrigger : IStepType;
