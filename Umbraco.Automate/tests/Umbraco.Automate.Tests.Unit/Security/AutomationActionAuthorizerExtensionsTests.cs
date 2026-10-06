using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;

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
}
