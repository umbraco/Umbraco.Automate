using Json.Schema;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class RequestApprovalActionTests
{
    private readonly RequestApprovalAction _action;

    public RequestApprovalActionTests()
    {
        var infrastructure = new ActionInfrastructure(Mock.Of<IEditableModelResolver>());
        _action = new RequestApprovalAction(infrastructure);
    }

    [Fact]
    public void Alias_Is_Correct()
    {
        _action.Alias.ShouldBe("umbracoAutomate.requestApproval");
    }

    [Fact]
    public void SettingsType_Is_RequestApprovalSettings()
    {
        _action.SettingsType.ShouldBe(typeof(RequestApprovalSettings));
    }

    [Fact]
    public void OutputType_Is_ApprovalDecisionOutput()
    {
        _action.OutputType.ShouldBe(typeof(ApprovalDecisionOutput));
    }

    [Fact]
    public void OutputSchema_Exposes_Approved_As_Boolean()
    {
        // The schema drives the backoffice binding picker, so the approval step only shows up as a
        // binding source if this is non-null. `approved` is the field conditions should branch on.
        var schema = _action.GetOutputSchema();

        schema.ShouldNotBeNull();
        var properties = schema.GetProperties();
        properties.ShouldNotBeNull();
        properties!.TryGetValue("approved", out var approvedSchema).ShouldBeTrue();
        approvedSchema!.GetJsonType().ShouldBe(SchemaValueType.Boolean);
    }

    [Fact]
    public void OutputSchema_Still_Exposes_Outcome_As_String()
    {
        // Kept alongside `approved` for backwards compatibility: conditions written against
        // `outcome` must keep resolving, and it must read as a word, not an enum index.
        var schema = _action.GetOutputSchema();

        schema.ShouldNotBeNull();
        var properties = schema.GetProperties();
        properties.ShouldNotBeNull();
        properties!.TryGetValue("outcome", out var outcomeSchema).ShouldBeTrue();
        outcomeSchema!.GetJsonType().ShouldBe(SchemaValueType.String);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_WaitForInput()
    {
        var runId = Guid.NewGuid();
        var stepId = Guid.NewGuid();

        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = runId,
            StepId = stepId,
            ActionAlias = _action.Alias,
            Settings = new RequestApprovalSettings { Prompt = "Please approve deployment" },
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.WaitingForInput);
        var suspension = result.Suspension.ShouldBeOfType<ActionSuspension.WaitForEvent>();
        suspension.EventName.ShouldBe("approval");
        suspension.EventKey.ShouldBe($"{runId}:{stepId}");
        result.OutputData.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithDefaultSettings_UsesDefaultPrompt()
    {
        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = _action.Alias,
            Settings = new RequestApprovalSettings(),
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.WaitingForInput);
    }
}
