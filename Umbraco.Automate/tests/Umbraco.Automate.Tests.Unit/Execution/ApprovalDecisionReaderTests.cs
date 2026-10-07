using System.Text.Json;
using Newtonsoft.Json.Linq;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// <see cref="ApprovalDecisionReader"/> runs inside WorkflowCore's error handling (via
/// <c>ActionWorkflowStep.PrimeForRetry</c>), so a payload that does not deserialize to a decision
/// must read as "no decision" rather than throw.
/// </summary>
public class ApprovalDecisionReaderTests
{
    public static TheoryData<object> MalformedPayloads => new()
    {
        // System.Text.Json: a number is not an object.
        JsonDocument.Parse("42").RootElement.Clone(),

        // Newtonsoft: an object where the enum outcome should be.
        JObject.Parse("""{ "outcome": { "nested": 1 } }"""),
    };

    [Theory]
    [MemberData(nameof(MalformedPayloads))]
    public void Read_MalformedPayload_ReturnsNoDecision(object payload)
        => ApprovalDecisionReader.Read(payload).ShouldBeNull();
}
