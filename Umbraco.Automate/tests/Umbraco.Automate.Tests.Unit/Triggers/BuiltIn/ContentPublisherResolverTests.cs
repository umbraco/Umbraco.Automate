using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ContentPublisherResolverTests
{
    private readonly Mock<IUserService> _userService = new();
    private readonly ILogger _logger = Mock.Of<ILogger>();

    [Fact]
    public void NullPublisherId_ReturnsNull()
        => ContentPublisherResolver.Resolve(_userService.Object, null, _logger).ShouldBeNull();

    [Fact]
    public void SuperUser_ReturnsSystem_WithoutLookup()
    {
        ContentPublisherResolver.Resolve(_userService.Object, -1, _logger)
            .ShouldBe(ContentPublisherKind.System);

        _userService.Verify(s => s.GetUserById(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void ApiUser_ReturnsApi()
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Kind).Returns(UserKind.Api);
        _userService.Setup(s => s.GetUserById(5)).Returns(user.Object);

        ContentPublisherResolver.Resolve(_userService.Object, 5, _logger)
            .ShouldBe(ContentPublisherKind.Api);
    }

    [Fact]
    public void DefaultUser_ReturnsUser()
    {
        var user = new Mock<IUser>();
        user.SetupGet(u => u.Kind).Returns(UserKind.Default);
        _userService.Setup(s => s.GetUserById(5)).Returns(user.Object);

        ContentPublisherResolver.Resolve(_userService.Object, 5, _logger)
            .ShouldBe(ContentPublisherKind.User);
    }

    [Fact]
    public void MissingUser_ReturnsNull()
    {
        _userService.Setup(s => s.GetUserById(5)).Returns((IUser?)null);

        ContentPublisherResolver.Resolve(_userService.Object, 5, _logger).ShouldBeNull();
    }

    [Fact]
    public void ServiceFailure_ReturnsNull()
    {
        _userService.Setup(s => s.GetUserById(5)).Throws(new InvalidOperationException("boom"));

        ContentPublisherResolver.Resolve(_userService.Object, 5, _logger).ShouldBeNull();
    }
}
