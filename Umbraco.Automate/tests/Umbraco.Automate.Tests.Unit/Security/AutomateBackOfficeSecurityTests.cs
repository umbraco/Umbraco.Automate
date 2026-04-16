using Moq;
using Shouldly;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Tests.Unit.Security;

public class AutomateBackOfficeSecurityTests
{
    [Fact]
    public void CurrentUser_ReturnsInjectedUser()
    {
        var user = Mock.Of<IUser>(u => u.Key == Guid.NewGuid());
        var security = new AutomateBackOfficeSecurity(user);

        security.CurrentUser.ShouldBeSameAs(user);
    }

    [Fact]
    public void IsAuthenticated_ReturnsTrue()
    {
        var user = Mock.Of<IUser>();
        var security = new AutomateBackOfficeSecurity(user);

        security.IsAuthenticated().ShouldBeTrue();
    }

    [Fact]
    public void UserHasSectionAccess_UserHasSection_ReturnsTrue()
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.AllowedSections).Returns(new[] { "content", "media", "automate" });

        var security = new AutomateBackOfficeSecurity(user.Object);

        security.UserHasSectionAccess("automate", user.Object).ShouldBeTrue();
    }

    [Fact]
    public void UserHasSectionAccess_UserLacksSection_ReturnsFalse()
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.AllowedSections).Returns(new[] { "content" });

        var security = new AutomateBackOfficeSecurity(user.Object);

        security.UserHasSectionAccess("automate", user.Object).ShouldBeFalse();
    }
}
