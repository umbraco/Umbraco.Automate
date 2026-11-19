using System.Text.Json;
using Umbraco.Automate.Core.Actions.BuiltIn;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Reads an <see cref="ApprovalDecision"/> from the event data WorkflowCore puts on an execution
/// pointer when the event a step waits for is published.
/// </summary>
internal static class ApprovalDecisionReader
{
    /// <summary>
    /// Returns the decision carried by <paramref name="eventData"/>, or <see langword="null"/> when the
    /// payload is not an approval decision — including a JSON payload that does not deserialize to one.
    /// The payload's shape depends on where it came from: the object itself when published in-process,
    /// a <see cref="JsonElement"/>, or a Newtonsoft <c>JObject</c> after WorkflowCore's persistence
    /// round-trip.
    /// </summary>
    /// <remarks>
    /// Never throws for a bad payload: it also runs inside WorkflowCore's error handling
    /// (<see cref="ActionWorkflowStep.PrimeForRetry"/>), where an exception would escape the engine's
    /// error pipeline instead of being treated as "no decision".
    /// </remarks>
    public static ApprovalDecision? Read(object? eventData)
    {
        try
        {
            return eventData switch
            {
                ApprovalDecision decision => decision,
                JsonElement json => JsonSerializer.Deserialize<ApprovalDecision>(json.GetRawText(), Dispatch.JsonOptions.Default),
                Newtonsoft.Json.Linq.JObject jObject => jObject.ToObject<ApprovalDecision>(),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is JsonException or Newtonsoft.Json.JsonException)
        {
            return null;
        }
    }
}
