using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.AuthorizationStatus;

namespace Umbraco.Automate.Tests.Unit.Security;

public class AutomationActionAuthorizerTests
{
    [Fact]
    public async Task AuthorizeContentAsync_returns_success_when_cms_authorises()
    {
        var (sut, content, _, _) = BuildSut(withUser: true);
        var key = Guid.NewGuid();

        content
            .Setup(s => s.AuthorizeAccessAsync(It.IsAny<IUser>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ContentAuthorizationStatus.Success);

        var result = await sut.AuthorizeContentAsync(key, new HashSet<string> { "Umb.Document.Read" }, default);

        result.Authorized.ShouldBeTrue();
        result.FailureReason.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizeContentAsync_returns_failure_with_path_reason_when_outside_start_node()
    {
        var (sut, content, _, _) = BuildSut(withUser: true);
        var key = Guid.NewGuid();

        content
            .Setup(s => s.AuthorizeAccessAsync(It.IsAny<IUser>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ContentAuthorizationStatus.UnauthorizedMissingPathAccess);

        var result = await sut.AuthorizeContentAsync(key, new HashSet<string> { "Umb.Document.Read" }, default);

        result.Authorized.ShouldBeFalse();
        result.FailureReason.ShouldContain("start-node path");
        result.FailureReason.ShouldContain(key.ToString());
    }

    [Fact]
    public async Task AuthorizeContentAsync_returns_failure_with_permission_reason_when_verb_denied()
    {
        var (sut, content, _, _) = BuildSut(withUser: true);
        var key = Guid.NewGuid();
        var permissions = new HashSet<string> { "Umb.Document.Publish" };

        content
            .Setup(s => s.AuthorizeAccessAsync(It.IsAny<IUser>(), It.IsAny<IEnumerable<Guid>>(), It.IsAny<ISet<string>>()))
            .ReturnsAsync(ContentAuthorizationStatus.UnauthorizedMissingPermissionAccess);

        var result = await sut.AuthorizeContentAsync(key, permissions, default);

        result.Authorized.ShouldBeFalse();
        result.FailureReason.ShouldContain("Umb.Document.Publish");
    }

    [Fact]
    public async Task AuthorizeContentAsync_returns_failure_when_no_backoffice_identity()
    {
        var (sut, _, _, _) = BuildSut(withUser: false);

        var result = await sut.AuthorizeContentAsync(Guid.NewGuid(), new HashSet<string>(), default);

        result.Authorized.ShouldBeFalse();
        result.FailureReason.ShouldContain("No backoffice identity");
    }

    [Fact]
    public async Task AuthorizeMediaAsync_returns_success_when_cms_authorises()
    {
        var (sut, _, media, _) = BuildSut(withUser: true);

        media
            .Setup(s => s.AuthorizeAccessAsync(It.IsAny<IUser>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(MediaAuthorizationStatus.Success);

        var result = await sut.AuthorizeMediaAsync(Guid.NewGuid(), default);

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizeMediaAsync_returns_failure_with_path_reason_when_outside_start_node()
    {
        var (sut, _, media, _) = BuildSut(withUser: true);
        var key = Guid.NewGuid();

        media
            .Setup(s => s.AuthorizeAccessAsync(It.IsAny<IUser>(), It.IsAny<IEnumerable<Guid>>()))
            .ReturnsAsync(MediaAuthorizationStatus.UnauthorizedMissingPathAccess);

        var result = await sut.AuthorizeMediaAsync(key, default);

        result.Authorized.ShouldBeFalse();
        result.FailureReason.ShouldContain("start-node path");
        result.FailureReason.ShouldContain(key.ToString());
    }

    [Fact]
    public async Task FilterAuthorizedContentAsync_returns_empty_for_empty_input()
    {
        var (sut, _, _, _) = BuildSut(withUser: true);

        var result = await sut.FilterAuthorizedContentAsync([], new HashSet<string>(), default);

        result.Count.ShouldBe(0);
    }

    [Fact]
    public async Task FilterAuthorizedContentAsync_returns_only_authorized_keys()
    {
        var (sut, content, _, _) = BuildSut(withUser: true);
        var allowed = Guid.NewGuid();
        var denied = Guid.NewGuid();

        // Per-key sequential calls — match by the first content key in the IEnumerable.
        content
            .Setup(s => s.AuthorizeAccessAsync(
                It.IsAny<IUser>(),
                It.Is<IEnumerable<Guid>>(keys => keys.First() == allowed),
                It.IsAny<ISet<string>>()))
            .ReturnsAsync(ContentAuthorizationStatus.Success);

        content
            .Setup(s => s.AuthorizeAccessAsync(
                It.IsAny<IUser>(),
                It.Is<IEnumerable<Guid>>(keys => keys.First() == denied),
                It.IsAny<ISet<string>>()))
            .ReturnsAsync(ContentAuthorizationStatus.UnauthorizedMissingPathAccess);

        var result = await sut.FilterAuthorizedContentAsync(
            [allowed, denied],
            new HashSet<string> { "Umb.Document.Read" },
            default);

        result.ShouldContain(allowed);
        result.ShouldNotContain(denied);
    }

    [Fact]
    public async Task FilterAuthorizedContentAsync_returns_empty_when_no_backoffice_identity()
    {
        var (sut, _, _, _) = BuildSut(withUser: false);

        var result = await sut.FilterAuthorizedContentAsync(
            [Guid.NewGuid(), Guid.NewGuid()],
            new HashSet<string>(),
            default);

        result.Count.ShouldBe(0);
    }

    private static (AutomationActionAuthorizer Sut,
                    Mock<IContentPermissionService> Content,
                    Mock<IMediaPermissionService> Media,
                    Mock<IBackOfficeSecurityAccessor> Accessor)
        BuildSut(bool withUser)
    {
        var content = new Mock<IContentPermissionService>();
        var media = new Mock<IMediaPermissionService>();
        var accessor = new Mock<IBackOfficeSecurityAccessor>();

        if (withUser)
        {
            var user = new Mock<IUser>();
            user.Setup(u => u.Key).Returns(Guid.NewGuid());

            var security = new Mock<IBackOfficeSecurity>();
            security.Setup(s => s.CurrentUser).Returns(user.Object);
            accessor.Setup(a => a.BackOfficeSecurity).Returns(security.Object);
        }
        else
        {
            accessor.Setup(a => a.BackOfficeSecurity).Returns((IBackOfficeSecurity?)null);
        }

        var sut = new AutomationActionAuthorizer(
            content.Object,
            media.Object,
            accessor.Object,
            NullLogger<AutomationActionAuthorizer>.Instance);

        return (sut, content, media, accessor);
    }
}
