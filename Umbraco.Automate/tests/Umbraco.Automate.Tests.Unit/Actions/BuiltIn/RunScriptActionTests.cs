using Json.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Scripting;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

[Collection("Scripting")]
public class RunScriptActionTests
{
    [Fact]
    public void HasCorrectAlias()
    {
        CreateAction().Alias.ShouldBe("umbracoAutomate.runScript");
    }

    [Fact]
    public void HasSettingsType()
    {
        CreateAction().SettingsType.ShouldBe(typeof(RunScriptSettings));
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsScriptResultAsOutput()
    {
        var action = CreateAction();
        var context = CreateContext(
            new RunScriptSettings { Script = "export default function (data) { return data.n * 2 }" },
            new Dictionary<string, object?> { ["n"] = 21 });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData.ShouldBeOfType<RunScriptOutput>();
        output.Result!.GetValue<int>().ShouldBe(42);
    }

    [Fact]
    public async Task ExecuteAsync_ExposesStepOutputsByAliasAtBindingPaths()
    {
        var mediaOutput = new Dictionary<string, object?>
        {
            ["properties"] = new Dictionary<string, object?> { ["umbracoBytes"] = 2048L },
        };
        var context = CreateContext(
            new RunScriptSettings
            {
                Script = "export default function (data) { return data.steps.getMedia.properties.umbracoBytes / 1024 }",
            },
            bindingData: CreateBindingData(steps: Steps((Guid.NewGuid(), "getMedia", mediaOutput))));

        var result = await CreateAction().ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<RunScriptOutput>().Result!.GetValue<int>().ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_ExposesTriggerPreviousAndLoop()
    {
        var previous = new Dictionary<string, object?> { ["result"] = "prev" };
        var bindingData = CreateBindingData(
            trigger: new Dictionary<string, object?> { ["name"] = "Home" },
            steps: Steps((Guid.NewGuid(), "first", previous)));
        bindingData["previous"] = previous;
        bindingData["loop"] = new Dictionary<string, object?> { ["item"] = "a", ["index"] = 3 };

        var context = CreateContext(
            new RunScriptSettings
            {
                Script = "export default (d) => [d.trigger.name, d.previous.result, d.loop.item, d.loop.index].join('|')",
            },
            bindingData: bindingData);

        var result = await CreateAction().ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<RunScriptOutput>().Result!.GetValue<string>().ShouldBe("Home|prev|a|3");
    }

    [Fact]
    public async Task ExecuteAsync_AliasedStepIsListedOnce_UnaliasedStepByGuid()
    {
        var unaliasedId = Guid.NewGuid();
        var context = CreateContext(
            new RunScriptSettings { Script = "export default (d) => Object.keys(d.steps).sort()" },
            bindingData: CreateBindingData(steps: Steps(
                (Guid.NewGuid(), "named", new Dictionary<string, object?> { ["x"] = 1 }),
                (unaliasedId, null, new Dictionary<string, object?> { ["y"] = 2 }))));

        var result = await CreateAction().ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        var keys = result.OutputData.ShouldBeOfType<RunScriptOutput>().Result!.AsArray()
            .Select(n => n!.GetValue<string>())
            .ToArray();
        keys.ShouldBe(new[] { unaliasedId.ToString(), "named" }.Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task ExecuteAsync_InputMappingsAreRootKeysAndWinOverBindingContext()
    {
        var context = CreateContext(
            new RunScriptSettings { Script = "export default (d) => d.name + '/' + d.trigger" },
            new Dictionary<string, object?> { ["name"] = "mapped", ["trigger"] = "override" },
            CreateBindingData(trigger: new Dictionary<string, object?> { ["name"] = "Home" }));

        var result = await CreateAction().ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<RunScriptOutput>().Result!.GetValue<string>().ShouldBe("mapped/override");
    }

    [Fact]
    public async Task ExecuteAsync_NormalisesPersistedJTokensAndPocos()
    {
        // After a WorkflowCore persistence round-trip outputs can surface as JTokens, and a trigger
        // can put a POCO in its output — both must reach the script as plain JSON.
        var context = CreateContext(
            new RunScriptSettings { Script = "export default (d) => d.steps.jt.items[1].v + ':' + d.trigger.poco.method" },
            bindingData: CreateBindingData(
                trigger: new Dictionary<string, object?> { ["poco"] = new { Method = "POST" } },
                steps: Steps((Guid.NewGuid(), "jt", new Dictionary<string, object?>
                {
                    ["items"] = Newtonsoft.Json.Linq.JArray.Parse("""[{ "v": 1 }, { "v": 2 }]"""),
                }))));

        var result = await CreateAction().ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<RunScriptOutput>().Result!.GetValue<string>().ShouldBe("2:POST");
    }

    [Fact]
    public async Task ExecuteAsync_PropertyAccessIsCaseSensitive()
    {
        // Documented behaviour: unlike bindings, script paths must match the alias casing.
        var context = CreateContext(
            new RunScriptSettings { Script = "export default (d) => [typeof d.steps.getMedia, typeof d.steps.getmedia].join('|')" },
            bindingData: CreateBindingData(steps: Steps((Guid.NewGuid(), "getMedia", new Dictionary<string, object?> { ["x"] = 1 }))));

        var result = await CreateAction().ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<RunScriptOutput>().Result!.GetValue<string>().ShouldBe("object|undefined");
    }

    [Fact]
    public async Task ExecuteAsync_MutatingDataDoesNotChangeBindingContext()
    {
        var output = new Dictionary<string, object?> { ["value"] = "original" };
        var context = CreateContext(
            new RunScriptSettings { Script = "export default (d) => { d.steps.s.value = 'changed'; return d.steps.s.value }" },
            bindingData: CreateBindingData(steps: Steps((Guid.NewGuid(), "s", output))));

        var result = await CreateAction().ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        output["value"].ShouldBe("original");
    }

    [Fact]
    public async Task ExecuteAsync_SelfReferencingData_ReturnsValidationFailure()
    {
        var cyclic = new Dictionary<string, object?>();
        cyclic["self"] = cyclic;
        var context = CreateContext(
            new RunScriptSettings { Script = "export default (d) => 1" },
            bindingData: CreateBindingData(trigger: cyclic));

        var result = await CreateAction().ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyScript_ReturnsValidationFailure()
    {
        var action = CreateAction();
        var context = CreateContext(new RunScriptSettings { Script = "  " });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSyntax_ReturnsValidationFailure()
    {
        var action = CreateAction();
        var context = CreateContext(new RunScriptSettings { Script = "export default function ( {" });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ExecuteAsync_ThrownError_ReturnsUnknownFailure()
    {
        var action = CreateAction();
        var context = CreateContext(
            new RunScriptSettings { Script = "export default function () { throw new Error('boom') }" });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Unknown);
    }

    [Fact]
    public async Task ExecuteAsync_InfiniteLoop_ReturnsTimeoutFailure()
    {
        var action = CreateAction();
        var context = CreateContext(
            new RunScriptSettings { Script = "export default function () { while (true) { let x = 1 } }" });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Timeout);
    }

    [Fact]
    public async Task ExecuteAsync_ActionDisabled_ReturnsConfigurationError()
    {
        var action = CreateAction(new ScriptingOptions { Enabled = false });
        var context = CreateContext(new RunScriptSettings { Script = "export default function () { return 1 }" });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);
    }

    [Fact]
    public async Task ExecuteAsync_FetchDisabledGlobally_FetchIsUndefinedEvenWhenStepAllows()
    {
        var action = CreateAction(new ScriptingOptions { FetchEnabled = false });
        var context = CreateContext(new RunScriptSettings
        {
            Script = "export default function () { return typeof fetch }",
            AllowFetch = true,
        });

        var result = await action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<RunScriptOutput>().Result!.GetValue<string>().ShouldBe("undefined");
    }

    [Fact]
    public async Task ValidateSettingsAsync_InvalidScript_ReturnsErrors()
    {
        var errors = await CreateAction().ValidateSettingsAsync(new RunScriptSettings { Script = "export default function ( {" });
        errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ValidateSettingsAsync_ValidScript_ReturnsNoErrors()
    {
        var errors = await CreateAction().ValidateSettingsAsync(new RunScriptSettings { Script = "export default (d) => d" });
        errors.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetOutputSchemaAsync_ConfiguredSchema_DescribesResultProperties()
    {
        IStepType action = CreateAction();

        var schema = await action.GetOutputSchemaAsync(new Dictionary<string, object?>
        {
            ["script"] = "export default (d) => ({ upper: d.name.toUpperCase() })",
            ["outputSchema"] = """{ "type": "object", "properties": { "upper": { "type": "string" } } }""",
        });

        schema.ShouldNotBeNull();

        // The configured shape sits under the reserved `result` property, so downstream steps bind
        // ${ steps.<alias>.result.upper }.
        var result = schema!.GetProperties()!["result"];
        result.GetProperties()!.Keys.ShouldContain("upper");
    }

    [Fact]
    public async Task GetOutputSchemaAsync_NoConfiguredSchema_FallsBackToStaticSchema()
    {
        IStepType action = CreateAction();

        var schema = await action.GetOutputSchemaAsync(new Dictionary<string, object?>
        {
            ["script"] = "export default (d) => d",
        });

        // `result` stays bindable, it just has no described shape.
        schema.ShouldNotBeNull();
        schema!.GetProperties()!.Keys.ShouldContain("result");
    }

    [Fact]
    public async Task GetOutputSchemaAsync_UnusableSchema_FallsBackToStaticSchema()
    {
        // Binding autocomplete must degrade quietly — save-time validation reports the problem.
        IStepType action = CreateAction();

        var schema = await action.GetOutputSchemaAsync(new Dictionary<string, object?>
        {
            ["script"] = "export default (d) => d",
            ["outputSchema"] = "{ not json",
        });

        schema.ShouldNotBeNull();
        schema!.GetProperties()!.Keys.ShouldContain("result");
    }

    [Fact]
    public async Task ValidateSettingsAsync_MalformedOutputSchema_ReturnsError()
    {
        var errors = await CreateAction().ValidateSettingsAsync(new RunScriptSettings
        {
            Script = "export default (d) => d",
            OutputSchema = "{ not json",
        });

        errors.ShouldHaveSingleItem().ShouldContain("Output schema");
    }

    [Fact]
    public async Task ValidateSettingsAsync_NonObjectOutputSchema_ReturnsError()
    {
        var errors = await CreateAction().ValidateSettingsAsync(new RunScriptSettings
        {
            Script = "export default (d) => d",
            OutputSchema = "\"just a string\"",
        });

        errors.ShouldHaveSingleItem().ShouldContain("must be a JSON object");
    }

    [Fact]
    public async Task ValidateSettingsAsync_ValidOutputSchema_ReturnsNoErrors()
    {
        var errors = await CreateAction().ValidateSettingsAsync(new RunScriptSettings
        {
            Script = "export default (d) => d",
            OutputSchema = """{ "type": "object", "properties": { "upper": { "type": "string" } } }""",
        });

        errors.ShouldBeEmpty();
    }

    private static ActionContext CreateContext(
        RunScriptSettings settings,
        IReadOnlyDictionary<string, object?>? inputData = null,
        IReadOnlyDictionary<string, object?>? bindingData = null) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.runScript",
        Settings = settings,
        InputData = inputData ?? new Dictionary<string, object?>(),
        BindingData = bindingData,
    };

    /// <summary>
    /// Mirrors the shape <c>BindingDataBuilder</c> produces for a run.
    /// </summary>
    private static Dictionary<string, object?> CreateBindingData(
        Dictionary<string, object?>? trigger = null,
        Dictionary<string, object?>? steps = null) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["trigger"] = trigger ?? new Dictionary<string, object?>(),
        ["steps"] = steps ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
    };

    /// <summary>
    /// Registers each output under its GUID and, when present, its alias — the same object under
    /// both keys, as <c>BindingDataBuilder</c> does.
    /// </summary>
    private static Dictionary<string, object?> Steps(params (Guid Id, string? Alias, Dictionary<string, object?> Output)[] steps)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (id, alias, output) in steps)
        {
            dict[id.ToString()] = output;
            if (alias is not null)
            {
                dict[alias] = output;
            }
        }

        return dict;
    }

    private static RunScriptAction CreateAction(ScriptingOptions? scripting = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

        var executor = new ScriptExecutor(factory.Object, NullLogger<ScriptExecutor>.Instance);

        // A real resolver, not a mock: output-schema resolution goes through ResolveSettings, which
        // a mocked resolver would return null from.
        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()));
        var infrastructure = new ActionInfrastructure(modelResolver);
        return new RunScriptAction(
            infrastructure,
            executor,
            new ScriptValidator(),
            Options.Create(scripting ?? new ScriptingOptions()),
            Options.Create(new ExecutionOptions()),
            NullLogger<RunScriptAction>.Instance);
    }
}
