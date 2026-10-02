using Moq;
using Shouldly;
using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Security;

/// <summary>
/// Covers the default interface implementations that keep pre-existing implementers of
/// <see cref="IAutomationActionAuthorizer"/> compiling.
/// </summary>
public class AutomationActionAuthorizerDefaultsTests
{
    private static readonly IReadOnlySet<string> Permissions = new HashSet<string> { "Umb.Document.Read" };

    private static Mock<IAutomationActionAuthorizer> BuildSut() => new() { CallBase = true };

    [Fact]
    public async Task AuthorizeContentRootAsync_fails_closed_by_default()
    {
        var result = await BuildSut().Object.AuthorizeContentRootAsync(Permissions, default);

        result.Authorized.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AuthorizeMediaRootAsync_fails_closed_by_default()
    {
        var result = await BuildSut().Object.AuthorizeMediaRootAsync(default);

        result.Authorized.ShouldBeFalse();
        result.FailureReason.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task AuthorizeContentParentAsync_delegates_to_node_check_when_parent_given()
    {
        var sut = BuildSut();
        var key = Guid.NewGuid();
        sut.Setup(a => a.AuthorizeContentAsync(key, Permissions, default))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        var result = await sut.Object.AuthorizeContentParentAsync(key, Permissions, default);

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizeContentParentAsync_delegates_to_root_check_when_parent_is_null()
    {
        var sut = BuildSut();
        sut.Setup(a => a.AuthorizeContentRootAsync(Permissions, default))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        var result = await sut.Object.AuthorizeContentParentAsync(null, Permissions, default);

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizeMediaParentAsync_delegates_to_node_check_when_parent_given()
    {
        var sut = BuildSut();
        var key = Guid.NewGuid();
        sut.Setup(a => a.AuthorizeMediaAsync(key, default))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        var result = await sut.Object.AuthorizeMediaParentAsync(key, default);

        result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizeMediaParentAsync_delegates_to_root_check_when_parent_is_null()
    {
        var sut = BuildSut();
        sut.Setup(a => a.AuthorizeMediaRootAsync(default))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        var result = await sut.Object.AuthorizeMediaParentAsync(null, default);

        result.Authorized.ShouldBeTrue();
    }
}
