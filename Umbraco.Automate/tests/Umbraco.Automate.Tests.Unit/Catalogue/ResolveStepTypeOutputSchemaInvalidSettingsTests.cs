// S8 — Invalid settings return a clear error from output schema too (docs/plans/action-outcomes/STORIES.md)

using Json.Schema;
using Json.Schema.Generation;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Controllers;
using Umbraco.Automate.Web.Api.Management.Catalogue.Models;
using ActionContext = Umbraco.Automate.Core.Actions.ActionContext;
using ActionResult = Umbraco.Automate.Core.Actions.ActionResult;

namespace Umbraco.Automate.Tests.Unit.Catalogue;

public class ResolveStepTypeOutputSchemaInvalidSettingsTests
{
    private const string ResolverMessage = "Validation failed for model 'test.settings':\nName is required";

    #region Given valid settings

    [Fact]
    public async Task ResolveOutputSchema_ValidSettings_StillReturnsOk()
    {
        var result = await ResolveAsync(resolverThrows: false);

        result.ShouldBeOfType<OkObjectResult>();
    }

    #endregion

    #region Sad path: settings the settings type rejects

    [Fact]
    public async Task ResolveOutputSchema_InvalidSettings_ReturnsBadRequest()
    {
        var result = await ResolveAsync(resolverThrows: true);

        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ResolveOutputSchema_InvalidSettings_ProblemTitleIsInvalidSettings()
    {
        var result = await ResolveAsync(resolverThrows: true);

        ProblemOf(result).Title.ShouldBe("Invalid settings");
    }

    [Fact]
    public async Task ResolveOutputSchema_InvalidSettings_ProblemDetailIsTheResolverMessage()
    {
        var result = await ResolveAsync(resolverThrows: true);

        ProblemOf(result).Detail.ShouldBe(ResolverMessage);
    }

    #endregion

    #region Sad path: a bug in the action itself

    [Fact]
    public async Task ResolveOutputSchema_ActionOverrideThrowsInvalidOperation_PropagatesTheException()
    {
        var resolver = new Mock<IEditableModelResolver>();
        resolver.Setup(r => r.ResolveModel<TestSettings>(
                It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<EditableModelSchema?>()))
            .Returns(new TestSettings { Name = "x" });
        var action = new ThrowingAction(new ActionInfrastructure(resolver.Object));
        var controller = new ResolveStepTypeOutputSchemaController(
            new ActionCollection(() => [action]),
            new ControlFlowCollection(() => []),
            new TriggerCollection(() => []));

        var exception = await Record.ExceptionAsync(() => controller.ResolveOutputSchema(
            "test.throwing",
            new ResolveOutputSchemaRequestModel
            {
                Settings = new Dictionary<string, object?> { ["name"] = "x" },
            }));

        exception.ShouldBeOfType<InvalidOperationException>();
    }

    #endregion

    private static ProblemDetails ProblemOf(IActionResult result)
        => result.ShouldBeOfType<BadRequestObjectResult>().Value.ShouldBeOfType<ProblemDetails>();

    private static Task<IActionResult> ResolveAsync(bool resolverThrows)
    {
        var resolver = new Mock<IEditableModelResolver>();
        var setup = resolver.Setup(r => r.ResolveModel<TestSettings>(
            It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<EditableModelSchema?>()));

        if (resolverThrows)
        {
            setup.Throws(new SettingsResolutionException(ResolverMessage));
        }
        else
        {
            setup.Returns(new TestSettings { Name = "x" });
        }

        var action = new SettingsAction(new ActionInfrastructure(resolver.Object));
        var controller = new ResolveStepTypeOutputSchemaController(
            new ActionCollection(() => [action]),
            new ControlFlowCollection(() => []),
            new TriggerCollection(() => []));

        return controller.ResolveOutputSchema(
            "test.settings",
            new ResolveOutputSchemaRequestModel
            {
                Settings = new Dictionary<string, object?> { ["name"] = "x" },
            });
    }

    private class TestSettings
    {
        public string? Name { get; set; }
    }

    [Action("test.settings", "Settings Action")]
    private class SettingsAction(ActionInfrastructure infrastructure)
        : DynamicOutputActionBase<TestSettings>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<JsonSchema?> GetOutputSchemaAsync(
            TestSettings? settings, CancellationToken cancellationToken)
            => Task.FromResult<JsonSchema?>(null);
    }

    [Action("test.throwing", "Throwing Action")]
    private class ThrowingAction(ActionInfrastructure infrastructure)
        : DynamicOutputActionBase<TestSettings>(infrastructure)
    {
        public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
            => throw new NotImplementedException();

        protected override Task<JsonSchema?> GetOutputSchemaAsync(
            TestSettings? settings, CancellationToken cancellationToken)
            => throw new InvalidOperationException("bug in the action");
    }
}
