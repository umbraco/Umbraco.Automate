using Json.Schema;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Automation.Controllers;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Automations;

public class TriggerAutomationControllerTests
{
    private const string TriggerAlias = "umbracoAutomate.webhook";

    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAutomationExecutor> _executor = new();
    private readonly Mock<ICircuitBreakerService> _circuitBreaker = new();

    public TriggerAutomationControllerTests()
    {
        _circuitBreaker
            .Setup(s => s.IsRunAllowedAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task TriggerAutomation_WithATriggerThatNeedsNoPayload_PassesNoTriggerOutputData()
    {
        var trigger = new FakeTrigger(ManualRunOutput.None);
        var controller = GivenController(trigger);
        var automation = GivenPublishedAutomation();

        var result = await controller.TriggerAutomation(automation.Id);

        result.ShouldBeOfType<AcceptedResult>();
        VerifyExecutedWith(automation, null);
    }

    [Fact]
    public async Task TriggerAutomation_WithATriggerThatDoesNotSupportManualRuns_PassesNoTriggerOutputData()
    {
        // Nothing to ask, so the automation is simply started bare.
        var controller = GivenController(new FakeTriggerWithoutManualRun());
        var automation = GivenPublishedAutomation();

        var result = await controller.TriggerAutomation(automation.Id);

        result.ShouldBeOfType<AcceptedResult>();
        VerifyExecutedWith(automation, null);
    }

    [Fact]
    public async Task TriggerAutomation_PassesTheTriggersStandInPayloadToTheExecutor()
    {
        var payload = new Dictionary<string, object?> { ["method"] = "POST", ["body"] = "{}" };
        var controller = GivenController(new FakeTrigger(ManualRunOutput.From(payload)));
        var automation = GivenPublishedAutomation();

        var result = await controller.TriggerAutomation(automation.Id);

        result.ShouldBeOfType<AcceptedResult>();
        VerifyExecutedWith(automation, payload);
    }

    [Fact]
    public async Task TriggerAutomation_WhenTheTriggerRejectsItsSettings_Returns400AndDoesNotExecute()
    {
        var controller = GivenController(new FakeTrigger(ManualRunOutput.Invalid("Headers are not a JSON object.")));
        var automation = GivenPublishedAutomation();

        var result = await controller.TriggerAutomation(automation.Id);

        var badRequest = result.ShouldBeOfType<BadRequestObjectResult>();
        badRequest.Value.ShouldBeOfType<ProblemDetails>().Detail.ShouldBe("Headers are not a JSON object.");
        _executor.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task TriggerAutomation_HandsTheResolvedTriggerSettingsToTheTrigger()
    {
        var resolvedSettings = new object();
        var trigger = new FakeTrigger(ManualRunOutput.None, resolvedSettings);
        var controller = GivenController(trigger);
        var automation = GivenPublishedAutomation(new Dictionary<string, object?> { ["allowedMethod"] = "GET" });

        await controller.TriggerAutomation(automation.Id);

        trigger.SettingsSeen.ShouldBeSameAs(resolvedSettings);
    }

    [Fact]
    public async Task TriggerAutomation_WithNoTriggerSettings_AsksTheTriggerWithNullSettings()
    {
        var trigger = new FakeTrigger(ManualRunOutput.None);
        var controller = GivenController(trigger);
        var automation = GivenPublishedAutomation();

        await controller.TriggerAutomation(automation.Id);

        trigger.WasAsked.ShouldBeTrue();
        trigger.SettingsSeen.ShouldBeNull();
    }

    [Fact]
    public async Task TriggerAutomation_NotPublished_Returns409AndDoesNotExecute()
    {
        var controller = GivenController(new FakeTrigger(ManualRunOutput.None));
        var automation = new AutomationBuilder()
            .WithStatus(AutomationStatus.Draft)
            .WithWebhookTrigger()
            .Build();
        _automationService
            .Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await controller.TriggerAutomation(automation.Id);

        result.ShouldBeOfType<ConflictObjectResult>();
        _executor.VerifyNoOtherCalls();
    }

    private TriggerAutomationController GivenController(ITrigger trigger)
    {
        var authorizationService = new Mock<IAuthorizationService>();
        authorizationService
            .Setup(s => s.AuthorizeAsync(
                It.IsAny<System.Security.Claims.ClaimsPrincipal>(),
                It.IsAny<object?>(),
                It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        return new TriggerAutomationController(
            _automationService.Object,
            authorizationService.Object,
            _executor.Object,
            _circuitBreaker.Object,
            Mock.Of<IBackOfficeSecurityAccessor>(),
            new TriggerCollection(() => [trigger]));
    }

    private Automation GivenPublishedAutomation(Dictionary<string, object?>? triggerSettings = null)
    {
        var builder = new AutomationBuilder().WithStatus(AutomationStatus.Published);
        var automation = triggerSettings is null
            ? builder.WithWebhookTrigger().Build()
            : builder.WithTrigger(TriggerAlias, triggerSettings).Build();

        _automationService
            .Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        return automation;
    }

    private void VerifyExecutedWith(Automation automation, Dictionary<string, object?>? expected)
        => _executor.Verify(
            e => e.ExecuteAsync(
                automation,
                TriggerInitiatorType.User,
                It.IsAny<string?>(),
                expected,
                It.IsAny<CancellationToken>(),
                It.IsAny<IReadOnlyList<Guid>?>()),
            Times.Once);

    /// <summary>
    /// Stands in for any trigger that opts into on-demand runs, so these tests cover the
    /// controller's own behaviour — does it ask, does it refuse, does it pass the payload on —
    /// rather than any one trigger's payload building.
    /// </summary>
    private sealed class FakeTrigger(ManualRunOutput output, object? resolvedSettings = null)
        : FakeTriggerWithoutManualRun, ISupportsManualRun
    {
        public bool WasAsked { get; private set; }

        public object? SettingsSeen { get; private set; }

        public override object? ResolveSettings(Dictionary<string, object?> settings) => resolvedSettings;

        public ManualRunOutput CreateManualRunOutput(object? settings)
        {
            WasAsked = true;
            SettingsSeen = settings;
            return output;
        }
    }

    private class FakeTriggerWithoutManualRun : ITrigger
    {
        public string Alias => TriggerAlias;

        public string Name => "Fake";

        public string? Description => null;

        public string? Group => null;

        public string? Icon => null;

        public string? ConnectionTypeAlias => null;

        public IReadOnlyList<string> RequiredSections => [];

        public IReadOnlyList<string> RequiredPermissions => [];

        public Type? SettingsType => null;

        public Type? OutputType => null;

        public bool HasDynamicOutputSchema => false;

        public EditableModelSchema? GetSettingsSchema() => null;

        public JsonSchema? GetOutputSchema() => null;

        public Task<JsonSchema?> GetOutputSchemaAsync(
            Dictionary<string, object?>? settings,
            CancellationToken cancellationToken = default) => Task.FromResult<JsonSchema?>(null);

        public virtual object? ResolveSettings(Dictionary<string, object?> settings) => null;
    }
}
