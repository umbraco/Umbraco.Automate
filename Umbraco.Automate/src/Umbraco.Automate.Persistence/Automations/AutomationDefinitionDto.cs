using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// DTO for the JSON-serialised automation definition (trigger + steps + connections + canvas).
/// </summary>
internal sealed class AutomationDefinitionDto
{
    public TriggerConfiguration? Trigger { get; set; }

    public IList<StepConfiguration> Steps { get; set; } = [];

    public IList<StepConnection> Connections { get; set; } = [];

    public string? CanvasState { get; set; }

    public AutomationNotificationSettings? NotificationSettings { get; set; }
}
