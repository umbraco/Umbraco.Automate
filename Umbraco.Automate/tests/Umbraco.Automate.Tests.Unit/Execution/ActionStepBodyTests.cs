using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.Middleware;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Diagnostics;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Execution.ControlFlow;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Events;
using WorkflowCore.Interface;
using WorkflowCore.Models;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// Covers how <see cref="ActionStepBody"/> resolves a step's settings before running the action,
/// in particular a step saved with no settings at all: the flow editor does not persist a default
/// the user left untouched, so the settings type's property-initializer defaults (bindings
/// included) must still be resolved.
/// </summary>
public class ActionStepBodyTests
{
    private readonly Mock<IAutomationRunRepository> _runRepository = new();
    private readonly EditableModelResolver _modelResolver =
        new(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()));

    private readonly List<StepRun> _addedStepRuns = [];

    public ActionStepBodyTests()
    {
        _runRepository
            .Setup(r => r.AddStepRunAsync(It.IsAny<StepRun>(), It.IsAny<CancellationToken>()))
            .Callback<StepRun, CancellationToken>((stepRun, _) => _addedStepRuns.Add(stepRun))
            .ReturnsAsync((StepRun stepRun, CancellationToken _) => stepRun);
    }

    [Fact]
    public async Task RunAsync_EmptySettings_ResolvesBindingsInInitializerDefaults()
    {
        var action = new BindingDefaultsAction(new ActionInfrastructure(_modelResolver));

        await CreateBody(action, new Dictionary<string, object?>())
            .RunAsync(CreateContext(new Dictionary<string, object?> { ["formId"] = "form-123" }));

        var settings = action.ReceivedSettings.ShouldBeOfType<BindingDefaultsSettings>();
        settings.FormId.ShouldBe("form-123");
        settings.Label.ShouldBe("Static label");
    }

    [Fact]
    public async Task RunAsync_EmptySettings_GetSettingsReturnsResolvedInstance()
    {
        var action = new BindingDefaultsAction(new ActionInfrastructure(_modelResolver));

        await CreateBody(action, new Dictionary<string, object?>())
            .RunAsync(CreateContext(new Dictionary<string, object?> { ["formId"] = "form-123" }));

        action.SettingsFromGetSettings.ShouldNotBeNull();
        action.SettingsFromGetSettings.FormId.ShouldBe("form-123");
    }

    [Fact]
    public async Task RunAsync_ConfiguredSettings_OverrideInitializerDefaultsAndResolveBindings()
    {
        var action = new BindingDefaultsAction(new ActionInfrastructure(_modelResolver));

        await CreateBody(action, new Dictionary<string, object?> { ["formId"] = "${ trigger.otherFormId }" })
            .RunAsync(CreateContext(new Dictionary<string, object?>
            {
                ["formId"] = "form-123",
                ["otherFormId"] = "form-456",
            }));

        action.ReceivedSettings.ShouldBeOfType<BindingDefaultsSettings>().FormId.ShouldBe("form-456");
    }

    [Fact]
    public async Task RunAsync_EmptySettings_RecordsResolvedDefaultsAsStepInputWithSecretsMasked()
    {
        var action = new BindingDefaultsAction(new ActionInfrastructure(_modelResolver));

        await CreateBody(action, new Dictionary<string, object?>())
            .RunAsync(CreateContext(new Dictionary<string, object?>
            {
                ["formId"] = "form-123",
                ["token"] = "leak-me-if-you-can",
            }));

        var stepRun = _addedStepRuns.ShouldHaveSingleItem();
        stepRun.Status.ShouldBe(StepRunStatus.Completed);
        stepRun.InputData.ShouldNotBeNull();
        stepRun.InputData.ShouldContain("form-123");
        stepRun.InputData.ShouldNotContain("leak-me-if-you-can");
    }

    [Fact]
    public async Task RunAsync_EmptySettings_MissingRequiredDefault_FailsSetupAsConfigurationError()
    {
        var action = new RequiredSettingAction(new ActionInfrastructure(_modelResolver));

        // The builder's Terminate error behavior rethrows the setup failure to WorkflowCore.
        await Should.ThrowAsync<InvalidOperationException>(
            () => CreateBody(action, new Dictionary<string, object?>()).RunAsync(CreateContext([])));

        action.Executed.ShouldBeFalse();
        var stepRun = _addedStepRuns.ShouldHaveSingleItem();
        stepRun.Status.ShouldBe(StepRunStatus.Failed);
        stepRun.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);
    }

    [Fact]
    public async Task RunAsync_ActionWithoutSettingsType_LeavesSettingsNullAndRecordsInputs()
    {
        var action = new NoSettingsAction(new ActionInfrastructure(_modelResolver));
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias(action.Alias)
            .WithName("No settings")
            .WithInputMapping("formId", "${ trigger.formId }");

        await CreateBody(action, stepConfig)
            .RunAsync(CreateContext(new Dictionary<string, object?> { ["formId"] = "form-123" }));

        action.Executed.ShouldBeTrue();
        action.ReceivedSettings.ShouldBeNull();
        _addedStepRuns.ShouldHaveSingleItem().InputData.ShouldNotBeNull().ShouldContain("form-123");
    }

    private ActionStepBody CreateBody(IAction action, Dictionary<string, object?> settings)
    {
        StepConfiguration stepConfig = new StepConfigurationBuilder()
            .WithActionAlias(action.Alias)
            .WithName("Step")
            .WithSettings(settings);
        return CreateBody(action, stepConfig);
    }

    private ActionStepBody CreateBody(IAction action, StepConfiguration stepConfig)
    {
        var bindingEvaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        var hydrationCache = new StepOutputHydrationCache(_runRepository.Object);

        return new ActionStepBody(
            stepConfig,
            action,
            new ActionMiddlewarePipeline(new ActionMiddlewareCollection(Array.Empty<IActionMiddleware>)),
            bindingEvaluator,
            new ForEachCollectionCache(bindingEvaluator, hydrationCache),
            hydrationCache,
            new SettingsBindingResolver(bindingEvaluator),
            _runRepository.Object,
            Mock.Of<IConnectionService>(),
            new DefaultStepErrorClassifier(),
            Options.Create(new ExecutionOptions()),
            new AutomateMetrics(CreateMeterFactory()),
            Mock.Of<IEventAggregator>(),
            Mock.Of<ILogger<ActionStepBody>>());
    }

    private static IStepExecutionContext CreateContext(Dictionary<string, object?> triggerOutput)
    {
        var context = new Mock<IStepExecutionContext>();
        var workflow = new WorkflowInstance
        {
            Data = new AutomationWorkflowData
            {
                RunId = Guid.NewGuid(),
                AutomationId = Guid.NewGuid(),
                TriggerOutput = triggerOutput,
            },
        };
        context.Setup(c => c.Workflow).Returns(workflow);
        context.Setup(c => c.ExecutionPointer).Returns(new ExecutionPointer());
        context.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
        return context.Object;
    }

    private static IMeterFactory CreateMeterFactory()
    {
        var mock = new Mock<IMeterFactory>();
        mock.Setup(f => f.Create(It.IsAny<MeterOptions>()))
            .Returns((MeterOptions opts) => new Meter(opts.Name));
        return mock.Object;
    }

    public sealed class BindingDefaultsSettings
    {
        [Field(Label = "Form", SupportsBindings = true)]
        public string FormId { get; set; } = "${ trigger.formId }";

        [Field(Label = "Label")]
        public string Label { get; set; } = "Static label";

        [Field(Label = "Token", SupportsBindings = true, IsSensitive = true)]
        public string Token { get; set; } = "${ trigger.token }";
    }

    public sealed class RequiredSettings
    {
        [Field(Label = "Name")]
        [Required]
        public string? Name { get; set; }
    }

    [Action("test.bindingDefaults", "Binding Defaults")]
    private sealed class BindingDefaultsAction(ActionInfrastructure infrastructure)
        : ActionBase<BindingDefaultsSettings>(infrastructure)
    {
        public object? ReceivedSettings { get; private set; }

        public BindingDefaultsSettings? SettingsFromGetSettings { get; private set; }

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
        {
            ReceivedSettings = context.Settings;
            SettingsFromGetSettings = context.GetSettings<BindingDefaultsSettings>();
            return Task.FromResult(ActionResult.Success());
        }
    }

    [Action("test.requiredSetting", "Required Setting")]
    private sealed class RequiredSettingAction(ActionInfrastructure infrastructure)
        : ActionBase<RequiredSettings>(infrastructure)
    {
        public bool Executed { get; private set; }

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
        {
            Executed = true;
            return Task.FromResult(ActionResult.Success());
        }
    }

    [Action("test.noSettings", "No Settings")]
    private sealed class NoSettingsAction(ActionInfrastructure infrastructure)
        : ActionBase<object, object>(infrastructure)
    {
        public bool Executed { get; private set; }

        public object? ReceivedSettings { get; private set; }

        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
        {
            Executed = true;
            ReceivedSettings = context.Settings;
            return Task.FromResult(ActionResult.Success());
        }
    }
}
