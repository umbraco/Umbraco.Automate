using System.Text.Json;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Bindings.Filters;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Tests.Unit.Bindings;

public class BindingEvaluatorTests
{
    private readonly BindingEvaluator _evaluator = new(
        new BindingFilterCollection(() =>
        [
            new TruncateFilter(),
            new LowercaseFilter(),
            new UppercaseFilter(),
            new FallbackFilter(),
            new StripHtmlFilter(),
        ]));

    [Fact]
    public void Constructor_Throws_WhenTwoFiltersShareAnAlias()
    {
        // Filters are auto-discovered across all assemblies, so a colliding alias must
        // fail fast (and name both types) rather than silently shadow one filter.
        var act = () => new BindingEvaluator(
            new BindingFilterCollection(() => [new StripHtmlFilter(), new StripHtmlFilter()]));

        act.ShouldThrow<InvalidOperationException>().Message.ShouldContain("stripHtml");
    }

    private readonly Dictionary<string, object?> _data = new()
    {
        ["trigger"] = new Dictionary<string, object?>
        {
            ["contentName"] = "Hello World",
            ["contentKey"] = Guid.Empty,
            ["body"] = "<p>Some <b>HTML</b> content</p>",
            ["empty"] = null,
            ["cultures"] = new List<object?> { "en-US", "da-DK", "de-DE" },
            ["items"] = new List<object?>
            {
                new Dictionary<string, object?> { ["name"] = "First", ["id"] = (long)1 },
                new Dictionary<string, object?> { ["name"] = "Second", ["id"] = (long)2 },
            },
            ["headers"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["Content-Type"] = "application/json",
                ["X-Custom"] = "value",
            },
        },
        ["steps"] = new Dictionary<string, object?>
        {
            ["sendEmail"] = new Dictionary<string, object?>
            {
                ["messageId"] = "msg-123",
            },
            ["getContent"] = new Dictionary<string, object?>
            {
                ["name"] = "Test Page",
                ["properties"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    ["subtitle"] = "Hello",
                    ["body"] = "World",
                },
                ["availableCultures"] = new List<object?> { "en-US", "da-DK" },
            },
        },
    };

    [Fact]
    public void Evaluate_SimplePath()
    {
        var result = _evaluator.Evaluate("Name: ${ trigger.contentName }", _data);

        result.ShouldBe("Name: Hello World");
    }

    // Regression: when a workflow data round-trip goes through Newtonsoft without
    // TypeNameHandling, nested values come back as JObject/JArray. BindingEvaluator
    // must unwrap these to plain Dictionary/List so path traversal still works.
    [Fact]
    public void Evaluate_AfterNewtonsoftRoundTripWithoutTypeNames_ResolvesNestedPath()
    {
        var output = new Umbraco.Automate.Core.Actions.BuiltIn.FindContentOutput
        {
            Matches = [new Umbraco.Automate.Core.Actions.BuiltIn.FindContentMatch
            {
                ContentKey = Guid.Parse("776a648d-3a0d-4745-8853-2de74cd486d8"),
                Name = "Novicell UK",
                ContentTypeAlias = "partnerPage",
            }],
        };
        var stjJson = JsonSerializer.Serialize(output, JsonOptions.Default);
        var unwrapped = JsonOptions.DeserializeToUnwrappedDictionary(stjJson);

        // No TypeNameHandling — Newtonsoft produces JObject/JArray on deserialization.
        var persisted = Newtonsoft.Json.JsonConvert.SerializeObject(unwrapped);
        var rehydrated = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object?>>(persisted)!;

        var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["steps"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["findContent"] = rehydrated },
        };

        // Without the defensive fix, these evaluate to empty strings — which is what
        // triggers "Invalid or missing content key: ''" in the user's automation.
        var matches = _evaluator.Evaluate("${ steps.findContent.matches }", bindingData);
        var contentKey = _evaluator.Evaluate("${ steps.findContent.first.contentKey }", bindingData);

        matches.ShouldNotBeNullOrEmpty();
        contentKey.ShouldBe("776a648d-3a0d-4745-8853-2de74cd486d8");
    }

    // Simulates the exact WorkflowCore persistence round-trip on AutomationWorkflowData
    // as used inside a ForEach branch — this is where the user bug manifests.
    [Fact]
    public void Evaluate_AfterAutomationWorkflowDataRoundTrip_ResolvesNestedPath()
    {
        var findContentId = Guid.Parse("814d234d-16fc-4916-9cf8-ead07d3b0e72");
        var output = new Umbraco.Automate.Core.Actions.BuiltIn.FindContentOutput
        {
            Matches = [new Umbraco.Automate.Core.Actions.BuiltIn.FindContentMatch
            {
                ContentKey = Guid.Parse("776a648d-3a0d-4745-8853-2de74cd486d8"),
                Name = "Novicell UK",
                ContentTypeAlias = "partnerPage",
            }],
        };
        var stjJson = JsonSerializer.Serialize(output, JsonOptions.Default);
        var unwrapped = JsonOptions.DeserializeToUnwrappedDictionary(stjJson);

        var data = new Umbraco.Automate.Core.Execution.AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            AutomationAlias = "test",
            StepOutputs = new Dictionary<Guid, Dictionary<string, object?>> { [findContentId] = unwrapped },
            StepAliases = new Dictionary<Guid, string> { [findContentId] = "findContent" },
        };

        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
        };
        var persisted = Newtonsoft.Json.JsonConvert.SerializeObject(data, settings);
        var rehydrated = Newtonsoft.Json.JsonConvert.DeserializeObject<Umbraco.Automate.Core.Execution.AutomationWorkflowData>(persisted, settings)!;

        var bindingData = Umbraco.Automate.Core.Execution.BindingDataBuilder.Build(rehydrated);

        _evaluator.Evaluate("${ steps.findContent.first.contentKey }", bindingData)
            .ShouldBe("776a648d-3a0d-4745-8853-2de74cd486d8");

        var matches = _evaluator.Evaluate("${ steps.findContent.matches }", bindingData);
        matches.Length.ShouldBeGreaterThan(2); // not "[]" or ""
    }

    // Regression for #112: after the WorkflowCore persistence round-trip through
    // Newtonsoft with TypeNameHandling.All, nested dictionaries are rebuilt with the
    // DEFAULT (case-sensitive) comparer — losing the OrdinalIgnoreCase comparer they
    // were created with. A PascalCase binding against a camelCase-stored key then
    // silently resolves to "". Path resolution must be case-insensitive regardless
    // of the dictionary's own comparer.
    [Fact]
    public void Evaluate_AfterAutomationWorkflowDataRoundTrip_ResolvesPascalCaseBindingOnCamelCaseKey()
    {
        var findContentId = Guid.Parse("814d234d-16fc-4916-9cf8-ead07d3b0e72");
        var output = new Umbraco.Automate.Core.Actions.BuiltIn.FindContentOutput
        {
            Matches = [new Umbraco.Automate.Core.Actions.BuiltIn.FindContentMatch
            {
                ContentKey = Guid.Parse("776a648d-3a0d-4745-8853-2de74cd486d8"),
                Name = "Novicell UK",
                ContentTypeAlias = "partnerPage",
            }],
        };
        var stjJson = JsonSerializer.Serialize(output, JsonOptions.Default);
        var unwrapped = JsonOptions.DeserializeToUnwrappedDictionary(stjJson);

        var data = new Umbraco.Automate.Core.Execution.AutomationWorkflowData
        {
            RunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            AutomationAlias = "test",
            StepOutputs = new Dictionary<Guid, Dictionary<string, object?>> { [findContentId] = unwrapped },
            StepAliases = new Dictionary<Guid, string> { [findContentId] = "findContent" },
        };

        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
        };
        var persisted = Newtonsoft.Json.JsonConvert.SerializeObject(data, settings);
        var rehydrated = Newtonsoft.Json.JsonConvert.DeserializeObject<Umbraco.Automate.Core.Execution.AutomationWorkflowData>(persisted, settings)!;

        var bindingData = Umbraco.Automate.Core.Execution.BindingDataBuilder.Build(rehydrated);

        // PascalCase binding against a camelCase-stored key: must still resolve.
        _evaluator.Evaluate("${ steps.findContent.first.ContentKey }", bindingData)
            .ShouldBe("776a648d-3a0d-4745-8853-2de74cd486d8");
    }

    // #112: ResolveKey must not depend on the dictionary's own comparer. A plain
    // ORDINAL dictionary (post-round-trip state) with a camelCase key must still
    // resolve a PascalCase segment via case-insensitive fallback.
    [Fact]
    public void ResolvePath_CaseInsensitiveFallback_OnOrdinalDictionary()
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["steps"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["findContent"] = new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["contentKey"] = "abc-123",
                },
            },
        };

        BindingEvaluator.ResolvePath("steps.findContent.ContentKey", data)
            .ShouldBe("abc-123");
    }

    // #112: exact match must win over a differing-case key when both are present.
    [Fact]
    public void ResolvePath_ExactMatch_WinsOverDifferingCase()
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["key"] = "lower",
            ["Key"] = "upper",
        };

        BindingEvaluator.ResolvePath("key", data).ShouldBe("lower");
        BindingEvaluator.ResolvePath("Key", data).ShouldBe("upper");
    }

    // #112: the exact TryGetValue must short-circuit before the case-insensitive scan,
    // so an exact match wins even when a differing-case key enumerates first.
    [Fact]
    public void ResolvePath_ExactMatch_WinsOverEarlierDifferingCaseKey()
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["KEY"] = "upper", // enumerates first, matches "key" only case-insensitively
            ["key"] = "lower", // the exact match the fallback scan must never reach
        };

        BindingEvaluator.ResolvePath("key", data).ShouldBe("lower");
    }

    // Repro for user-reported bug: step output with a nested object under "first"
    // doesn't resolve AFTER the workflow suspends and resumes, because WorkflowCore
    // persists the data dictionary through Newtonsoft.Json — which returns JObject/
    // JArray rather than Dictionary<string, object?> / List<object?>.
    [Fact]
    public void Evaluate_AfterNewtonsoftPersistenceRoundTrip_StillResolvesNestedPath()
    {
        var output = new Umbraco.Automate.Core.Actions.BuiltIn.FindContentOutput
        {
            Matches = [new Umbraco.Automate.Core.Actions.BuiltIn.FindContentMatch
            {
                ContentKey = Guid.Parse("776a648d-3a0d-4745-8853-2de74cd486d8"),
                Name = "Novicell UK",
                ContentTypeAlias = "partnerPage",
            }],
        };
        var stjJson = JsonSerializer.Serialize(output, JsonOptions.Default);
        var unwrapped = JsonOptions.DeserializeToUnwrappedDictionary(stjJson);

        // Simulate WorkflowCore persisting and rehydrating the data through
        // Newtonsoft with TypeNameHandling.All (see EFCoreWorkflowPersistenceProvider).
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            TypeNameHandling = Newtonsoft.Json.TypeNameHandling.All,
        };
        var persisted = Newtonsoft.Json.JsonConvert.SerializeObject(unwrapped, settings);
        var rehydrated = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object?>>(persisted, settings)!;

        var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["steps"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["findContent"] = rehydrated,
            },
        };

        _evaluator.Evaluate("${ steps.findContent.first.contentKey }", bindingData)
            .ShouldBe("776a648d-3a0d-4745-8853-2de74cd486d8");
    }

    // Repro for user-reported bug: step output with a nested object under "first"
    // doesn't resolve through the binding path steps.<alias>.first.contentKey.
    [Fact]
    public void Evaluate_StepOutputDeserializedFromJson_ResolvesNestedFirstContentKey()
    {
        var output = new Umbraco.Automate.Core.Actions.BuiltIn.FindContentOutput
        {
            Matches = [new Umbraco.Automate.Core.Actions.BuiltIn.FindContentMatch
            {
                ContentKey = Guid.Parse("776a648d-3a0d-4745-8853-2de74cd486d8"),
                Name = "Novicell UK",
                ContentTypeAlias = "partnerPage",
            }],
        };
        var json = JsonSerializer.Serialize(output, JsonOptions.Default);
        var unwrapped = JsonOptions.DeserializeToUnwrappedDictionary(json);

        var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["steps"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["findContent"] = unwrapped,
            },
        };

        _evaluator.Evaluate("${ steps.findContent.first.contentKey }", bindingData)
            .ShouldBe("776a648d-3a0d-4745-8853-2de74cd486d8");
    }

    // Repro for user-reported bug: IsNotEmpty on a list-typed binding stringifies
    // to something non-empty so the condition evaluates true.
    [Fact]
    public void Evaluate_StepOutputDeserializedFromJson_MatchesStringifiesNonEmpty()
    {
        var output = new Umbraco.Automate.Core.Actions.BuiltIn.FindContentOutput
        {
            Matches = [new Umbraco.Automate.Core.Actions.BuiltIn.FindContentMatch
            {
                ContentKey = Guid.NewGuid(),
                Name = "x",
                ContentTypeAlias = "t",
            }],
        };
        var json = JsonSerializer.Serialize(output, JsonOptions.Default);
        var unwrapped = JsonOptions.DeserializeToUnwrappedDictionary(json);

        var bindingData = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["steps"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["findContent"] = unwrapped },
        };

        var matches = _evaluator.Evaluate("${ steps.findContent.matches }", bindingData);
        matches.ShouldNotBeNullOrEmpty();
        matches.Length.ShouldBeGreaterThan(2); // "[]" would be 2
    }

    [Fact]
    public void Evaluate_NestedPath()
    {
        var result = _evaluator.Evaluate("ID: ${ steps.sendEmail.messageId }", _data);

        result.ShouldBe("ID: msg-123");
    }

    [Fact]
    public void Evaluate_WithFilter()
    {
        var result = _evaluator.Evaluate("${ trigger.contentName | lowercase }", _data);

        result.ShouldBe("hello world");
    }

    [Fact]
    public void Evaluate_WithChainedFilters()
    {
        var result = _evaluator.Evaluate("${ trigger.body | stripHtml | truncate:10:... }", _data);

        result.ShouldBe("Some HTML ...");
    }

    [Fact]
    public void Evaluate_FallbackOnNull()
    {
        var result = _evaluator.Evaluate("${ trigger.empty | fallback:N/A }", _data);

        result.ShouldBe("N/A");
    }

    [Fact]
    public void Evaluate_MissingPath_ReturnsEmpty()
    {
        var result = _evaluator.Evaluate("${ trigger.nonexistent }", _data);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Evaluate_PlainText_ReturnedUnchanged()
    {
        var result = _evaluator.Evaluate("Just plain text", _data);

        result.ShouldBe("Just plain text");
    }

    [Fact]
    public void Evaluate_MultipleBindings()
    {
        var result = _evaluator.Evaluate("${ trigger.contentName } (${ steps.sendEmail.messageId })", _data);

        result.ShouldBe("Hello World (msg-123)");
    }

    [Fact]
    public void ResolvePath_HandlesNestedDictionaries()
    {
        var result = BindingEvaluator.ResolvePath("steps.sendEmail.messageId", _data);

        result.ShouldBe("msg-123");
    }

    [Fact]
    public void ResolvePath_ReturnsNull_ForMissingKey()
    {
        var result = BindingEvaluator.ResolvePath("trigger.missing", _data);

        result.ShouldBeNull();
    }

    // --- Nested dictionary traversal ---

    [Fact]
    public void ResolvePath_NestedDictionary_TraversesMultipleLevels()
    {
        var result = BindingEvaluator.ResolvePath("steps.getContent.properties.subtitle", _data);

        result.ShouldBe("Hello");
    }

    [Fact]
    public void Evaluate_NestedDictionaryProperty()
    {
        var result = _evaluator.Evaluate("${ steps.getContent.properties.subtitle }", _data);

        result.ShouldBe("Hello");
    }

    // --- Array index access ---

    [Fact]
    public void ResolvePath_ArrayIndex_ReturnsElement()
    {
        var result = BindingEvaluator.ResolvePath("trigger.cultures[0]", _data);

        result.ShouldBe("en-US");
    }

    [Fact]
    public void ResolvePath_ArrayIndex_SecondElement()
    {
        var result = BindingEvaluator.ResolvePath("trigger.cultures[1]", _data);

        result.ShouldBe("da-DK");
    }

    [Fact]
    public void ResolvePath_ArrayIndex_OutOfBounds_ReturnsNull()
    {
        var result = BindingEvaluator.ResolvePath("trigger.cultures[99]", _data);

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolvePath_ArrayIndex_ThenNestedProperty()
    {
        var result = BindingEvaluator.ResolvePath("trigger.items[0].name", _data);

        result.ShouldBe("First");
    }

    [Fact]
    public void ResolvePath_ArrayIndex_SecondItem_NestedProperty()
    {
        var result = BindingEvaluator.ResolvePath("trigger.items[1].id", _data);

        result.ShouldBe((long)2);
    }

    [Fact]
    public void Evaluate_ArrayIndexInTemplate()
    {
        var result = _evaluator.Evaluate("First culture: ${ trigger.cultures[0] }", _data);

        result.ShouldBe("First culture: en-US");
    }

    // --- Bare numeric index ---

    [Fact]
    public void ResolvePath_BareNumericIndex()
    {
        var result = BindingEvaluator.ResolvePath("trigger.cultures.0", _data);

        result.ShouldBe("en-US");
    }

    // --- Dictionary key access via brackets ---

    [Fact]
    public void ResolvePath_DictionaryKeyAccess_DoubleQuotes()
    {
        var result = BindingEvaluator.ResolvePath("""trigger.headers["Content-Type"]""", _data);

        result.ShouldBe("application/json");
    }

    [Fact]
    public void ResolvePath_DictionaryKeyAccess_SingleQuotes()
    {
        var result = BindingEvaluator.ResolvePath("trigger.headers['X-Custom']", _data);

        result.ShouldBe("value");
    }

    [Fact]
    public void Evaluate_DictionaryKeyAccessInTemplate()
    {
        var result = _evaluator.Evaluate("""Type: ${ trigger.headers["Content-Type"] }""", _data);

        result.ShouldBe("Type: application/json");
    }

    // --- Length / count pseudo-property ---

    [Fact]
    public void ResolvePath_Length_ReturnsListCount()
    {
        var result = BindingEvaluator.ResolvePath("trigger.cultures.length", _data);

        result.ShouldBe(3);
    }

    [Fact]
    public void ResolvePath_Count_ReturnsListCount()
    {
        var result = BindingEvaluator.ResolvePath("trigger.items.count", _data);

        result.ShouldBe(2);
    }

    [Fact]
    public void Evaluate_LengthInTemplate()
    {
        var result = _evaluator.Evaluate("${ trigger.cultures.length } cultures", _data);

        result.ShouldBe("3 cultures");
    }

    // --- Stringify ---

    [Fact]
    public void Evaluate_ListValue_SerializesToJsonArray()
    {
        var result = _evaluator.Evaluate("${ trigger.cultures }", _data);

        result.ShouldBe("""["en-US","da-DK","de-DE"]""");
    }

    [Fact]
    public void Evaluate_DictionaryValue_SerializesToJsonObject()
    {
        var result = _evaluator.Evaluate("${ steps.getContent.properties }", _data);

        result.ShouldContain("subtitle");
        result.ShouldContain("Hello");
    }

    // --- JSON string fallback (EnsureUnwrapped) ---

    [Fact]
    public void ResolvePath_JsonStringFallback_ObjectTraversal()
    {
        // Simulates a value that survived as a raw JSON string (e.g. from Newtonsoft round-trip).
        var data = new Dictionary<string, object?>
        {
            ["step"] = new Dictionary<string, object?>
            {
                ["output"] = """{"subtitle":"Hello","body":"World"}""",
            },
        };

        var result = BindingEvaluator.ResolvePath("step.output.subtitle", data);

        result.ShouldBe("Hello");
    }

    [Fact]
    public void ResolvePath_JsonStringFallback_ArrayIndex()
    {
        var data = new Dictionary<string, object?>
        {
            ["step"] = new Dictionary<string, object?>
            {
                ["items"] = """["alpha","beta","gamma"]""",
            },
        };

        var result = BindingEvaluator.ResolvePath("step.items[1]", data);

        result.ShouldBe("beta");
    }

    // --- Previous binding ---

    [Fact]
    public void Evaluate_PreviousBinding_ResolvesLastStepOutput()
    {
        var data = new Dictionary<string, object?>
        {
            ["trigger"] = new Dictionary<string, object?> { ["name"] = "test" },
            ["steps"] = new Dictionary<string, object?>(),
            ["previous"] = new Dictionary<string, object?>
            {
                ["statusCode"] = (long)200,
                ["body"] = "OK",
            },
        };

        var result = _evaluator.Evaluate("Status: ${ previous.statusCode }", data);

        result.ShouldBe("Status: 200");
    }

    [Fact]
    public void Evaluate_StepAlias_ResolvesOutputField()
    {
        // The steps dict already uses string keys, so aliases work via normal key lookup.
        var result = _evaluator.Evaluate("${ steps.sendEmail.messageId }", _data);

        result.ShouldBe("msg-123");
    }

    // --- End-to-end: simulates GetContentOutput serialization round-trip ---

    [Fact]
    public void ResolvePath_GetContentOutput_RoundTrip()
    {
        // Simulate what ActionStepBody.StoreOutputData does:
        // serialize GetContentOutput → JSON → DeserializeToUnwrappedDictionary.
        var outputJson = JsonSerializer.Serialize(new
        {
            ContentKey = Guid.Empty,
            Name = "Test Page",
            Properties = new Dictionary<string, object?>
            {
                ["subtitle"] = "My Subtitle",
                ["tags"] = new[] { "tag1", "tag2" },
            },
            AvailableCultures = new[] { "en-US", "da-DK" },
        }, JsonOptions.Default);

        var stepOutputs = JsonOptions.DeserializeToUnwrappedDictionary(outputJson);

        var data = new Dictionary<string, object?>
        {
            ["steps"] = new Dictionary<string, object?>
            {
                ["abc-123"] = stepOutputs,
            },
        };

        // Nested property access.
        BindingEvaluator.ResolvePath("steps.abc-123.properties.subtitle", data)
            .ShouldBe("My Subtitle");

        // Array index on a property within the nested dict.
        BindingEvaluator.ResolvePath("steps.abc-123.properties.tags[0]", data)
            .ShouldBe("tag1");

        // Array index on a top-level array.
        BindingEvaluator.ResolvePath("steps.abc-123.availableCultures[1]", data)
            .ShouldBe("da-DK");

        // Array length.
        BindingEvaluator.ResolvePath("steps.abc-123.availableCultures.length", data)
            .ShouldBe(2);
    }
}
