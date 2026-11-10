using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Extensions;

namespace Umbraco.Automate.Tests.Unit.Security;

public class AutomationActionAuthorizerExtensionsTests
{
    [Fact]
    public void ToFailedActionResult_carries_the_reason_in_an_unauthorized_exception()
    {
        var result = AutomationAuthorizationResult.Fail("Out of start-node path.").ToFailedActionResult();

        result.Exception.ShouldBeOfType<UnauthorizedAccessException>().Message.ShouldBe("Out of start-node path.");
    }

    [Fact]
    public void ToFailedActionResult_is_categorised_as_authentication()
    {
        var result = AutomationAuthorizationResult.Fail("Nope.").ToFailedActionResult();

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    [Fact]
    public async Task AuthorizeElementAsync_passes_the_permission_letters_to_the_authorizer_as_a_set()
    {
        var elementKey = Guid.NewGuid();
        IReadOnlySet<string>? captured = null;
        var authorizer = new Mock<IAutomationActionAuthorizer>();
        authorizer
            .Setup(a => a.AuthorizeElementAsync(elementKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, IReadOnlySet<string>, CancellationToken>((_, permissions, _) => captured = permissions)
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        IReadOnlyList<string> letters = ["A", "B"];
        await authorizer.Object.AuthorizeElementAsync(elementKey, letters, CancellationToken.None);

        captured.ShouldNotBeNull().ShouldBe(["A", "B"], ignoreOrder: true);
    }

    [Fact]
    public async Task AuthorizeElementAsync_returns_the_authorizer_result_unconverted()
    {
        var notFound = AutomationAuthorizationResult.NotFound("Missing.");
        var authorizer = new Mock<IAutomationActionAuthorizer>();
        authorizer
            .Setup(a => a.AuthorizeElementAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(notFound);

        IReadOnlyList<string> letters = ["A"];
        var result = await authorizer.Object.AuthorizeElementAsync(Guid.NewGuid(), letters, CancellationToken.None);

        result.IsNotFound.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizeElementParentAsync_passes_the_folder_and_permission_letters_to_the_authorizer()
    {
        var folderKey = Guid.NewGuid();
        IReadOnlySet<string>? captured = null;
        var authorizer = new Mock<IAutomationActionAuthorizer>();
        authorizer
            .Setup(a => a.AuthorizeElementParentAsync(folderKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .Callback<Guid?, IReadOnlySet<string>, CancellationToken>((_, permissions, _) => captured = permissions)
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        IReadOnlyList<string> letters = ["N"];
        var result = await authorizer.Object.AuthorizeElementParentAsync(folderKey, letters, CancellationToken.None);

        result.Authorized.ShouldBeTrue();
        captured.ShouldNotBeNull().ShouldBe(["N"]);
    }

    [Fact]
    public async Task AuthorizeElementParentAsync_returns_the_authorizer_result_unconverted()
    {
        var authorizer = new Mock<IAutomationActionAuthorizer>();
        authorizer
            .Setup(a => a.AuthorizeElementParentAsync(null, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.NotFound("Missing."));

        IReadOnlyList<string> letters = ["N"];
        var result = await authorizer.Object.AuthorizeElementParentAsync(null, letters, CancellationToken.None);

        result.IsNotFound.ShouldBeTrue();
    }
}
