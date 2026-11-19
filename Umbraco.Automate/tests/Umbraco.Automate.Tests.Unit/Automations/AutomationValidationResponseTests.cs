using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Management.Automation.Controllers;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Automate.Web.Api.Management.Versioning.Controllers;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Automations;

/// <summary>
/// An <see cref="AutomationValidationException"/> from create, update, publish, import or
/// rollback is a client error
/// with the reasons listed, not an unhandled 500.
/// </summary>
public class AutomationValidationResponseTests
{
    private static readonly AutomationValidationException ValidationException = new(
        "Step alias validation failed.",
        ["Step 'Loop' uses reserved alias 'loop'."]);

    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAuthorizationService> _authorizationService = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly Mock<IUmbracoMapper> _mapper = new();

    public AutomationValidationResponseTests()
    {
        _authorizationService
            .Setup(a => a.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var user = new Mock<IUser>();
        user.Setup(u => u.Key).Returns(Guid.NewGuid());
        var security = new Mock<IBackOfficeSecurity>();
        security.Setup(s => s.CurrentUser).Returns(user.Object);
        _securityAccessor.Setup(a => a.BackOfficeSecurity).Returns(security.Object);
    }

    [Fact]
    public async Task Create_WithReservedStepAlias_ReturnsValidationProblem()
    {
        _mapper.Setup(m => m.Map<Automation>(It.IsAny<object>())).Returns(new AutomationBuilder().Build());
        _automationService
            .Setup(s => s.CreateAutomationAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ValidationException);
        var controller = WithContext(new CreateAutomationController(
            _automationService.Object, _authorizationService.Object, _securityAccessor.Object, _mapper.Object));

        var result = await controller.CreateAutomation(new CreateAutomationRequestModel
        {
            Alias = "withLoop",
            Name = "With loop",
            WorkspaceId = Guid.NewGuid(),
        });

        ShouldBeValidationProblem(result);
    }

    [Fact]
    public async Task Update_WithReservedStepAlias_ReturnsValidationProblem()
    {
        var existing = GivenExisting();
        _automationService
            .Setup(s => s.UpdateAutomationAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ValidationException);
        var controller = WithContext(new UpdateAutomationController(
            _automationService.Object, _authorizationService.Object, _securityAccessor.Object, _mapper.Object));

        var result = await controller.UpdateAutomation(existing.Id, new UpdateAutomationRequestModel
        {
            Alias = existing.Alias,
            Name = existing.Name,
            Version = existing.Version,
        });

        ShouldBeValidationProblem(result);
    }

    [Fact]
    public async Task Publish_WithInvalidStepSettings_ReturnsValidationProblem()
    {
        var existing = GivenExisting();
        _automationService
            .Setup(s => s.PublishAutomationAsync(existing.Id, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ValidationException);
        var controller = WithContext(new PublishAutomationController(
            _automationService.Object, _authorizationService.Object, _securityAccessor.Object));

        var result = await controller.PublishAutomation(existing.Id);

        ShouldBeValidationProblem(result);
    }

    [Fact]
    public async Task ImportNew_WithReservedStepAlias_ReturnsValidationProblem()
    {
        GivenImportThrows();
        var controller = WithContext(new ImportNewAutomationController(
            _automationService.Object, _authorizationService.Object));

        var result = await controller.ImportNewAutomation(new ImportAutomationRequestModel
        {
            WorkspaceId = Guid.NewGuid(),
            ExportModel = null!,
        });

        ShouldBeValidationProblem(result);
    }

    [Fact]
    public async Task ImportExisting_WithReservedStepAlias_ReturnsValidationProblem()
    {
        var existing = GivenExisting();
        GivenImportThrows();
        var controller = WithContext(new ImportExistingAutomationController(
            _automationService.Object, _authorizationService.Object));

        var result = await controller.ImportExistingAutomation(existing.Id, null!);

        ShouldBeValidationProblem(result);
    }

    [Fact]
    public async Task Rollback_ToVersionThatFailsValidation_ReturnsValidationProblem()
    {
        var existing = new AutomationBuilder().AsDraft().Build();
        var adapter = new Mock<IVersionableEntityAdapter>();
        adapter.Setup(a => a.EntityTypeName).Returns("Automation");
        adapter.Setup(a => a.GetEntityAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        adapter
            .Setup(a => a.RollbackAsync(existing.Id, 2, It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ValidationException);
        var controller = WithContext(new EntityVersionHistoryController(
            Mock.Of<IEntityVersionService>(),
            new VersionableEntityAdapterCollection(() => [adapter.Object]),
            _authorizationService.Object,
            _securityAccessor.Object,
            _mapper.Object));

        var result = await controller.RollbackToVersion("Automation", existing.Id, 2);

        ShouldBeValidationProblem(result);
    }

    private void GivenImportThrows()
        => _automationService
            .Setup(s => s.ImportAutomationAsync(
                It.IsAny<AutomationExportModel>(),
                It.IsAny<Guid>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(ValidationException);

    private Automation GivenExisting()
    {
        var automation = new AutomationBuilder().AsDraft().Build();
        _automationService.Setup(s => s.GetAutomationAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        return automation;
    }

    private static TController WithContext<TController>(TController controller)
        where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }

    private static void ShouldBeValidationProblem(IActionResult result)
    {
        var objectResult = result.ShouldBeOfType<UnprocessableEntityObjectResult>();
        var problem = objectResult.Value.ShouldBeOfType<ProblemDetails>();
        problem.Detail.ShouldBe("Step 'Loop' uses reserved alias 'loop'.");
        problem.Extensions["errors"].ShouldBe(ValidationException.Errors);
    }
}
