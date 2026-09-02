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
        IReadOnlyDictionary<string, object?>? inputData = null) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.runScript",
        Settings = settings,
        InputData = inputData ?? new Dictionary<string, object?>(),
    };

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
