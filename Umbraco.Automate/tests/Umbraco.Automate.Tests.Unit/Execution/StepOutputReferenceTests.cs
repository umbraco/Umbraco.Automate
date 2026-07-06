using Newtonsoft.Json;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class StepOutputReferenceTests
{
    [Fact]
    public void CreateInlineOrMarker_OutputAtThreshold_InlinesUnwrappedDictionary()
    {
        // Pad the JSON to exactly the threshold — "at threshold" must stay inline.
        var json = PadJson("""{"message":"hello"}""", targetBytes: 256);
        var result = StepOutputReference.CreateInlineOrMarker(json, Guid.NewGuid(), maxInlineBytes: 256);

        result.ShouldNotContainKey(StepOutputReference.MarkerKey);
        result["message"].ShouldBe("hello");
    }

    [Fact]
    public void CreateInlineOrMarker_OutputOneByteOverThreshold_StoresMarker()
    {
        var stepRunId = Guid.NewGuid();
        var json = PadJson("""{"message":"hello"}""", targetBytes: 257);

        var result = StepOutputReference.CreateInlineOrMarker(json, stepRunId, maxInlineBytes: 256);

        result.Count.ShouldBe(1);
        StepOutputReference.TryGetStepRunId(result, out var resolved).ShouldBeTrue();
        resolved.ShouldBe(stepRunId);
    }

    [Fact]
    public void CreateInlineOrMarker_ThresholdMeasuresUtf8Bytes_NotChars()
    {
        // 100 three-byte characters: 121 chars of JSON but 321 bytes — the byte count decides.
        var json = $$"""{"message":"{{new string('€', 100)}}"}""";
        json.Length.ShouldBeLessThan(200);

        var result = StepOutputReference.CreateInlineOrMarker(json, Guid.NewGuid(), maxInlineBytes: 200);

        StepOutputReference.TryGetStepRunId(result, out _).ShouldBeTrue();
    }

    [Fact]
    public void TryGetStepRunId_DictionaryWithExtraKeys_IsNotAMarker()
    {
        var dict = new Dictionary<string, object?>
        {
            [StepOutputReference.MarkerKey] = Guid.NewGuid().ToString(),
            ["other"] = 1,
        };

        StepOutputReference.TryGetStepRunId(dict, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryGetStepRunId_SingleKeyDictionaryWithOtherKey_IsNotAMarker()
    {
        var dict = new Dictionary<string, object?> { ["message"] = "hello" };

        StepOutputReference.TryGetStepRunId(dict, out _).ShouldBeFalse();
    }

    [Fact]
    public void TryGetStepRunId_MarkerKeyWithNonGuidValue_IsNotAMarker()
    {
        var dict = new Dictionary<string, object?> { [StepOutputReference.MarkerKey] = "not-a-guid" };

        StepOutputReference.TryGetStepRunId(dict, out _).ShouldBeFalse();
    }

    [Fact]
    public void Marker_SurvivesWorkflowCorePersistenceRoundTrip()
    {
        // Same serializer settings as EFCoreWorkflowPersistenceProvider uses for the
        // workflow instance blob — the marker must round-trip and still be detected.
        var settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.All,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        };

        var stepId = Guid.NewGuid();
        var stepRunId = Guid.NewGuid();
        var data = new AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            StepOutputs = new Dictionary<Guid, Dictionary<string, object?>>
            {
                [stepId] = StepOutputReference.CreateMarker(stepRunId),
            },
        };

        var json = JsonConvert.SerializeObject(data, settings);
        var rehydrated = JsonConvert.DeserializeObject<AutomationWorkflowData>(json, settings)!;

        StepOutputReference.TryGetStepRunId(rehydrated.StepOutputs[stepId], out var resolved).ShouldBeTrue();
        resolved.ShouldBe(stepRunId);
    }

    /// <summary>
    /// Pads a JSON object with a filler property so its UTF-8 byte count lands exactly
    /// on <paramref name="targetBytes"/>.
    /// </summary>
    private static string PadJson(string json, int targetBytes)
    {
        const string prefix = ",\"pad\":\"";
        var overhead = prefix.Length + 1; // closing quote + closing brace - the brace stripped below
        var padLength = targetBytes - System.Text.Encoding.UTF8.GetByteCount(json) - overhead;
        padLength.ShouldBeGreaterThanOrEqualTo(0);

        var padded = json[..^1] + prefix + new string('x', padLength) + "\"}";
        System.Text.Encoding.UTF8.GetByteCount(padded).ShouldBe(targetBytes);
        return padded;
    }
}
