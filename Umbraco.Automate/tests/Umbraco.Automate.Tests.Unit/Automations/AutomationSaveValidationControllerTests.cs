using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Web.Api.Management.Automation.Controllers;
using Umbraco.Automate.Web.Api.Management.Automation.Models;
using Umbraco.Cms.Core.Mapping;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Automations;

/// <summary>
/// The save endpoints must translate <see cref="AutomationValidationException"/> into a 422 with
/// the individual errors — without a catch it surfaces as an unhandled 500 and the editor is told
/// nothing about what was wrong.
/// </summary>
public class AutomationSaveValidationControllerTests
{
    private static readonly IReadOnlyList<string> Errors =
    [
        "Step 'Fetch data': Unexpected token '{'.",
    ];

    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IAuthorizationService> _authorizationService = new();
    private readonly Mock<IBackOfficeSecurityAccessor> _securityAccessor = new();
    private readonly Mock<IUmbracoMapper> _mapper = new();

    public AutomationSaveValidationControllerTests()
    {
        _authorizationService
            .Setup(s => s.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object?>(), It.IsAny<string>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var user = new Mock<IUser>();
        user.Setup(u => u.Key).Returns(Guid.NewGuid());
        var security = new Mock<IBackOfficeSecurity>();
        security.Setup(s => s.CurrentUser).Returns(user.Object);
        _securityAccessor.Setup(a => a.BackOfficeSecurity).Returns(security.Object);
    }

    [Fact]
    public async Task CreateAutomation_InvalidStepSettings_Returns422WithErrors()
    {
        var automation = new Automation { Alias = "test", Name = "Test", WorkspaceId = Guid.NewGuid() };
        _mapper.Setup(m => m.Map<Automation>(It.IsAny<object>())).Returns(automation);
        _automationService
            .Setup(s => s.CreateAutomationAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AutomationValidationException("Cannot save automation 'Test'.", Errors));

        var controller = WithHttpContext(new CreateAutomationController(
            _automationService.Object,
            _authorizationService.Object,
            _securityAccessor.Object,
            _mapper.Object));

        var result = await controller.CreateAutomation(new CreateAutomationRequestModel
        {
            Alias = "test",
            Name = "Test",
            WorkspaceId = automation.WorkspaceId,
        });

        ShouldBeValidationProblem(result);
    }

    [Fact]
    public async Task UpdateAutomation_InvalidStepSettings_Returns422WithErrors()
    {
        var existing = new Automation { Alias = "test", Name = "Test", WorkspaceId = Guid.NewGuid() };
        _automationService
            .Setup(s => s.GetAutomationAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _automationService
            .Setup(s => s.UpdateAutomationAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AutomationValidationException("Cannot save automation 'Test'.", Errors));

        var controller = WithHttpContext(new UpdateAutomationController(
            _automationService.Object,
            _authorizationService.Object,
            _securityAccessor.Object,
            _mapper.Object));

        var result = await controller.UpdateAutomation(existing.Id, new UpdateAutomationRequestModel
        {
            Alias = "test",
            Name = "Test",
            Version = existing.Version,
        });

        ShouldBeValidationProblem(result);
    }

    private static void ShouldBeValidationProblem(IActionResult result)
    {
        var problem = result.ShouldBeOfType<UnprocessableEntityObjectResult>()
            .Value.ShouldBeOfType<ProblemDetails>();

        problem.Status.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        problem.Detail.ShouldContain("Cannot save automation");
        problem.Extensions["errors"].ShouldBe(Errors);
    }

    private static TController WithHttpContext<TController>(TController controller)
        where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };
        return controller;
    }
}
