using System.Text.Json;

namespace Umbraco.Automate.Web.Api.Management.Automation.Models;

/// <summary>
/// Request model for manually triggering an automation.
/// </summary>
public sealed class TriggerAutomationRequestModel
{
    /// <summary>
    /// Gets the trigger output data to expose to the automation's steps, standing in for the
    /// payload the real trigger would have produced. Shape it like the trigger's output model —
    /// for the built-in webhook trigger that means <c>method</c>, <c>body</c>, <c>headers</c>
    /// and <c>query</c>. Omit to run with no trigger data.
    /// </summary>
    public Dictionary<string, JsonElement>? TriggerOutputData { get; init; }
}
