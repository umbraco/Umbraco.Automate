using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Testing;
using Umbraco.Automate.Testing.Builders;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class StartAutomationActionTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IEntityVersionService> _versionService = new();
    private readonly Mock<IAutomationExecutor> _executor = new();

    private readonly Guid _workspaceId = Guid.NewGuid();
    private readonly Guid _parentAutomationId = Guid.NewGuid();
    private readonly Guid _parentRunId = Guid.NewGuid();

    [Fact]
    public void HasCorrectAlias()
        => CreateAction().Alias.ShouldBe("umbracoAutomate.startAutomation");

    [Fact]
    public void HasCorrectName()
        => CreateAction().Name.ShouldBe("Start Automation");

    [Fact]
    public void HasSettingsType()
        => CreateAction().SettingsType.ShouldBe(typeof(StartAutomationSettings));

    [Fact]
    public void HasOutputType()
        => CreateAction().OutputType.ShouldBe(typeof(StartAutomationOutput));

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public async Task ExecuteAsync_InvalidAutomationKey_FailsWithValidation(string key)
    {
        var result = await ExecuteAsync(new StartAutomationSettings { AutomationKey = key });

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2, 3]")]
    [InlineData("\"just a string\"")]
    public async Task ExecuteAsync_InvalidTriggerData_FailsWithValidation(string triggerData)
    {
        var target = CreateTargetAutomation();
        SetupAutomation(target);

        var result = await ExecuteAsync(new StartAutomationSettings
        {
            AutomationKey = target.Id.ToString(),
            TriggerData = triggerData,
        });

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        VerifyNoRunStarted();
    }

    [Fact]
    public async Task ExecuteAsync_AutomationNotFound_FailsWithConfigurationError()
    {
        var result = await ExecuteAsync(new StartAutomationSettings { AutomationKey = Guid.NewGuid().ToString() });

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);
    }

    [Fact]
    public async Task ExecuteAsync_AutomationInOtherWorkspace_FailsWithConfigurationError()
    {
        var target = new AutomationBuilder().WithWorkspaceId(Guid.NewGuid()).Build();
        SetupAutomation(target);

        var result = await ExecuteAsync(new StartAutomationSettings { AutomationKey = target.Id.ToString() });

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);
        VerifyNoRunStarted();
    }

    [Fact]
    public async Task ExecuteAsync_AutomationNotPublished_FailsWithConfigurationError()
    {
        var target = new AutomationBuilder().WithWorkspaceId(_workspaceId).AsDraft().Build();
        SetupAutomation(target);

        var result = await ExecuteAsync(new StartAutomationSettings { AutomationKey = target.Id.ToString() });

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);
        VerifyNoRunStarted();
    }

    [Fact]
    public async Task ExecuteAsync_SelfStart_FailsAsCycle()
    {
        var target = CreateTargetAutomation(id: _parentAutomationId);
        SetupAutomation(target);

        var result = await ExecuteAsync(new StartAutomationSettings { AutomationKey = target.Id.ToString() });

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);
        VerifyNoRunStarted();
    }

    [Fact]
    public async Task ExecuteAsync_TargetInOriginChain_FailsAsCycle()
    {
        var target = CreateTargetAutomation();
        SetupAutomation(target);

        var result = await ExecuteAsync(
            new StartAutomationSettings { AutomationKey = target.Id.ToString() },
            originChain: [target.Id]);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);
        VerifyNoRunStarted();
    }

    [Fact]
    public async Task ExecuteAsync_ChainDepthExceeded_FailsWithConfigurationError()
    {
        var target = CreateTargetAutomation();
        SetupAutomation(target);

        // Inherited chain already at MaxChainDepth (5) — adding this automation exceeds it.
        var inheritedChain = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToArray();

        var result = await ExecuteAsync(
            new StartAutomationSettings { AutomationKey = target.Id.ToString() },
            originChain: inheritedChain);

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.ConfigurationError);
        VerifyNoRunStarted();
    }

    [Fact]
    public async Task ExecuteAsync_StartsRun_WithSystemInitiatorAndExtendedChain()
    {
        var target = CreateTargetAutomation();
        SetupAutomation(target);
        var childRunId = Guid.NewGuid();
        _executor.Setup(e => e.ExecuteAsync(
                It.IsAny<Automation>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(childRunId);

        var upstream = Guid.NewGuid();
        var result = await ExecuteAsync(
            new StartAutomationSettings { AutomationKey = target.Id.ToString() },
            originChain: [upstream]);

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData.ShouldBeOfType<StartAutomationOutput>();
        output.Started.ShouldBeTrue();
        output.RunId.ShouldBe(childRunId);
        output.AutomationKey.ShouldBe(target.Id);

        _executor.Verify(e => e.ExecuteAsync(
            target,
            TriggerInitiatorType.System,
            _parentRunId.ToString(),
            null,
            It.IsAny<CancellationToken>(),
            It.Is<IReadOnlyList<Guid>>(chain => chain.SequenceEqual(new[] { upstream, _parentAutomationId }))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_RunsPublishedSnapshot_NotCurrentState()
    {
        var target = CreateTargetAutomation();
        SetupAutomation(target);

        var snapshot = new AutomationBuilder()
            .WithId(target.Id)
            .WithWorkspaceId(_workspaceId)
            .Build();
        _versionService.Setup(v => v.GetVersionSnapshotAsync<Automation>(target.Id, target.PublishedVersion!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(snapshot);

        await ExecuteAsync(new StartAutomationSettings { AutomationKey = target.Id.ToString() });

        _executor.Verify(e => e.ExecuteAsync(
            snapshot,
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(),
            It.IsAny<CancellationToken>(),
            It.IsAny<IReadOnlyList<Guid>>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_PassesParsedTriggerData()
    {
        var target = CreateTargetAutomation();
        SetupAutomation(target);

        Dictionary<string, object?>? captured = null;
        _executor.Setup(e => e.ExecuteAsync(
                It.IsAny<Automation>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()))
            .Callback<Automation, string, string?, Dictionary<string, object?>?, CancellationToken, IReadOnlyList<Guid>?>(
                (_, _, _, data, _, _) => captured = data)
            .ReturnsAsync(Guid.NewGuid());

        await ExecuteAsync(new StartAutomationSettings
        {
            AutomationKey = target.Id.ToString(),
            TriggerData = """{ "message": "hello", "count": 3 }""",
        });

        captured.ShouldNotBeNull();
        captured["message"].ShouldBe("hello");
        captured["count"].ShouldBe(3L);
    }

    [Fact]
    public async Task ExecuteAsync_CircuitBreakerSkip_SucceedsWithoutStarting()
    {
        var target = CreateTargetAutomation();
        SetupAutomation(target);
        _executor.Setup(e => e.ExecuteAsync(
                It.IsAny<Automation>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()))
            .ReturnsAsync(Guid.Empty);

        var result = await ExecuteAsync(new StartAutomationSettings { AutomationKey = target.Id.ToString() });

        result.Status.ShouldBe(ActionResultStatus.Success);
        var output = result.OutputData.ShouldBeOfType<StartAutomationOutput>();
        output.Started.ShouldBeFalse();
        output.RunId.ShouldBeNull();
        output.SkippedReason.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateSettingsAsync_InvalidKey_ReturnsError()
    {
        var errors = await CreateAction().ValidateSettingsAsync(
            new StartAutomationSettings { AutomationKey = "nope" });

        errors.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ValidateSettingsAsync_MissingAutomation_ReturnsError()
    {
        var errors = await CreateAction().ValidateSettingsAsync(
            new StartAutomationSettings { AutomationKey = Guid.NewGuid().ToString() });

        errors.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ValidateSettingsAsync_ValidSettings_ReturnsNoErrors()
    {
        var target = CreateTargetAutomation();
        SetupAutomation(target);

        var errors = await CreateAction().ValidateSettingsAsync(new StartAutomationSettings
        {
            AutomationKey = target.Id.ToString(),
            TriggerData = """{ "message": "hello" }""",
        });

        errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1, 2, 3]")]
    public async Task ValidateSettingsAsync_LiteralTriggerDataNotAnObject_ReturnsError(string triggerData)
    {
        var target = CreateTargetAutomation();
        SetupAutomation(target);

        var errors = await CreateAction().ValidateSettingsAsync(new StartAutomationSettings
        {
            AutomationKey = target.Id.ToString(),
            TriggerData = triggerData,
        });

        errors.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ValidateSettingsAsync_TriggerDataWithBindings_IsNotValidated()
    {
        var target = CreateTargetAutomation();
        SetupAutomation(target);

        // Bindings resolve at run time, so this is not parseable JSON yet — must not error.
        var errors = await CreateAction().ValidateSettingsAsync(new StartAutomationSettings
        {
            AutomationKey = target.Id.ToString(),
            TriggerData = """{ "contentKey": ${ trigger.contentKey } }""",
        });

        errors.ShouldBeEmpty();
    }

    private Automation CreateTargetAutomation(Guid? id = null)
        => new AutomationBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithWorkspaceId(_workspaceId)
            .Build();

    private void SetupAutomation(Automation automation)
        => _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

    private void VerifyNoRunStarted()
        => _executor.Verify(e => e.ExecuteAsync(
            It.IsAny<Automation>(), It.IsAny<string>(), It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>>()), Times.Never);

    private Task<ActionResult> ExecuteAsync(StartAutomationSettings settings, IReadOnlyList<Guid>? originChain = null)
        => ActionTestHarness.For<StartAutomationAction>()
            .WithService(_automationService.Object)
            .WithService(_versionService.Object)
            .WithService(_executor.Object)
            .WithService(CreateExecutionOptionsMonitor())
            .WithAutomationId(_parentAutomationId)
            .WithRunId(_parentRunId)
            .WithSettings(settings)
            .WithExecutionContext(new AutomationExecutionContext
            {
                ServiceAccountKey = Guid.NewGuid(),
                WorkspaceId = _workspaceId,
                WorkspaceName = "Test Workspace",
                AutomationId = _parentAutomationId,
                AutomationName = "Parent Automation",
                RunId = _parentRunId,
                InitiatorType = TriggerInitiatorType.System,
                AllowedConnections = [],
                OriginChain = originChain ?? [],
            })
            .ExecuteAsync();

    private StartAutomationAction CreateAction()
        => new(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _automationService.Object,
            _versionService.Object,
            _executor.Object,
            CreateExecutionOptionsMonitor(),
            Mock.Of<ILogger<StartAutomationAction>>());

    private static IOptionsMonitor<ExecutionOptions> CreateExecutionOptionsMonitor(int maxChainDepth = 5)
    {
        var monitor = new Mock<IOptionsMonitor<ExecutionOptions>>();
        monitor.SetupGet(m => m.CurrentValue).Returns(new ExecutionOptions { MaxChainDepth = maxChainDepth });
        return monitor.Object;
    }
}
