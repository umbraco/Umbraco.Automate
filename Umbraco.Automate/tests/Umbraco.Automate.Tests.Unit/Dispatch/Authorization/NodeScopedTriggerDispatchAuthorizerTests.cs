using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Tests.Unit.Dispatch.Authorization;

public class NodeScopedTriggerDispatchAuthorizerTests
{
    private readonly Mock<IAutomationActionAuthorizer> _nodeAuthorizer = new();
    private readonly NodeScopedTriggerDispatchAuthorizer _sut;

    public NodeScopedTriggerDispatchAuthorizerTests()
    {
        _sut = new NodeScopedTriggerDispatchAuthorizer(_nodeAuthorizer.Object);
    }

    [Fact]
    public async Task AuthorizeAsync_OutputWithoutMarker_ReturnsSuccess()
    {
        // Manual / scheduled / webhook outputs (or any output that doesn't implement
        // IContentScopedTriggerOutput / IMediaScopedTriggerOutput) must short-circuit to
        // Success without consulting IAutomationActionAuthorizer.
        var result = await _sut.AuthorizeAsync(
            BuildContext(typedOutput: new { Foo = "bar" }),
            CancellationToken.None);

        result.Authorized.ShouldBeTrue();
        _nodeAuthorizer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizeAsync_NullTypedOutput_ReturnsSuccess()
    {
        // Typed output is unavailable when the trigger declares no output type or
        // deserialisation failed. Failing closed would silently drop legitimate dispatches —
        // the dispatcher logs the deserialisation error elsewhere.
        var result = await _sut.AuthorizeAsync(
            BuildContext(typedOutput: null),
            CancellationToken.None);

        result.Authorized.ShouldBeTrue();
        _nodeAuthorizer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizeAsync_ContentOutputAllowed_DelegatesToContentAuthorizer()
    {
        // Happy path: content output's marker reports a key, IAutomationActionAuthorizer
        // returns Success → authoriser returns Success.
        var output = new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentName = "Home" };
        var user = Mock.Of<IUser>();

        _nodeAuthorizer
            .Setup(a => a.AuthorizeContentAsync(user, output.ContentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        var result = await _sut.AuthorizeAsync(BuildContext(output, user), CancellationToken.None);

        result.Authorized.ShouldBeTrue();
        _nodeAuthorizer.Verify(a => a.AuthorizeContentAsync(user, output.ContentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthorizeAsync_ContentOutputDenied_PropagatesFailureReason()
    {
        // Forbidden by start-node path — Fail() reason from the action authoriser must
        // surface so the dispatcher log line names the actual block reason.
        var output = new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentName = "Home" };

        _nodeAuthorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<IUser>(), output.ContentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Outside start-node path."));

        var result = await _sut.AuthorizeAsync(BuildContext(output), CancellationToken.None);

        result.Authorized.ShouldBeFalse();
        result.FailureReason.ShouldBe("Outside start-node path.");
    }

    [Fact]
    public async Task AuthorizeAsync_MediaOutput_RoutesToMediaAuthorizer()
    {
        // Media path uses AuthorizeMediaAsync — verb permissions don't apply, and content
        // routing must not fire.
        var output = new MediaSavedTriggerOutput { MediaKey = Guid.NewGuid(), MediaName = "Image.png" };
        var user = Mock.Of<IUser>();

        _nodeAuthorizer
            .Setup(a => a.AuthorizeMediaAsync(user, output.MediaKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        var result = await _sut.AuthorizeAsync(BuildContext(output, user), CancellationToken.None);

        result.Authorized.ShouldBeTrue();
        _nodeAuthorizer.Verify(a => a.AuthorizeMediaAsync(user, output.MediaKey, It.IsAny<CancellationToken>()), Times.Once);
        _nodeAuthorizer.Verify(a => a.AuthorizeContentAsync(It.IsAny<IUser>(), It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static TriggerDispatchAuthorizationContext BuildContext(object? typedOutput, IUser? user = null)
        => new()
        {
            // Trigger identity isn't consulted by the authoriser any more — the marker lives
            // on the output. Pass a mock so the required init-property is satisfied.
            Trigger = Mock.Of<ITrigger>(),
            TypedOutput = typedOutput,
            ServiceAccount = user ?? Mock.Of<IUser>(),
            Automation = new AutomationBuilder().WithTrigger("any").Build(),
        };
}
