using Microsoft.Extensions.Configuration;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Dispatch.Authorization;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Models.Membership;

namespace Umbraco.Automate.Tests.Unit.Dispatch.Authorization;

public class NodeScopedTriggerDispatchAuthorizerTests
{
    private readonly Mock<IAutomationActionAuthorizer> _nodeAuthorizer = new();
    private readonly NodeScopedTriggerDispatchAuthorizer _sut;
    private readonly TriggerInfrastructure _triggerInfra;

    public NodeScopedTriggerDispatchAuthorizerTests()
    {
        var modelResolver = new EditableModelResolver(new ConfigurationBuilder().Build());
        _triggerInfra = new TriggerInfrastructure(modelResolver);
        _sut = new NodeScopedTriggerDispatchAuthorizer(_nodeAuthorizer.Object);
    }

    [Fact]
    public async Task AuthorizeAsync_NonNodeScopedTrigger_ReturnsSuccess()
    {
        // Manual / scheduled / webhook triggers don't implement INodeScopedTrigger — the
        // authoriser must short-circuit to Success without consulting IAutomationActionAuthorizer.
        var trigger = new ManualTrigger(_triggerInfra);

        var result = await _sut.AuthorizeAsync(
            new TriggerDispatchAuthorizationContext
            {
                Trigger = trigger,
                TypedOutput = new { Foo = "bar" },
                ServiceAccount = Mock.Of<IUser>(),
                Automation = new AutomationBuilder().WithTrigger("umbracoAutomate.manual").Build(),
            },
            CancellationToken.None);

        result.Authorized.ShouldBeTrue();
        _nodeAuthorizer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizeAsync_NodeScopedTriggerWithoutTypedOutput_ReturnsSuccess()
    {
        // Typed output is unavailable when the trigger declares no output type or
        // deserialisation failed. Failing closed would silently drop legitimate dispatches —
        // the dispatcher logs the deserialisation error elsewhere.
        var trigger = new ContentSavedTrigger(_triggerInfra);

        var result = await _sut.AuthorizeAsync(
            new TriggerDispatchAuthorizationContext
            {
                Trigger = trigger,
                TypedOutput = null,
                ServiceAccount = Mock.Of<IUser>(),
                Automation = new AutomationBuilder().WithTrigger("umbracoAutomate.contentSaved").Build(),
            },
            CancellationToken.None);

        result.Authorized.ShouldBeTrue();
        _nodeAuthorizer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AuthorizeAsync_ContentTriggerAllowed_DelegatesToContentAuthorizer()
    {
        // Happy path: content trigger output identifies a node, IAutomationActionAuthorizer
        // returns Success → authoriser returns Success.
        var trigger = new ContentSavedTrigger(_triggerInfra);
        var output = new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentName = "Home" };
        var user = Mock.Of<IUser>();

        _nodeAuthorizer
            .Setup(a => a.AuthorizeContentAsync(user, output.ContentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        var result = await _sut.AuthorizeAsync(
            new TriggerDispatchAuthorizationContext
            {
                Trigger = trigger,
                TypedOutput = output,
                ServiceAccount = user,
                Automation = new AutomationBuilder().WithTrigger("umbracoAutomate.contentSaved").Build(),
            },
            CancellationToken.None);

        result.Authorized.ShouldBeTrue();
        _nodeAuthorizer.Verify(a => a.AuthorizeContentAsync(user, output.ContentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AuthorizeAsync_ContentTriggerDenied_PropagatesFailureReason()
    {
        // Forbidden by start-node path — Fail() reason from the action authoriser must
        // surface so the dispatcher log line names the actual block reason.
        var trigger = new ContentSavedTrigger(_triggerInfra);
        var output = new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid(), ContentName = "Home" };

        _nodeAuthorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<IUser>(), output.ContentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Outside start-node path."));

        var result = await _sut.AuthorizeAsync(
            new TriggerDispatchAuthorizationContext
            {
                Trigger = trigger,
                TypedOutput = output,
                ServiceAccount = Mock.Of<IUser>(),
                Automation = new AutomationBuilder().WithTrigger("umbracoAutomate.contentSaved").Build(),
            },
            CancellationToken.None);

        result.Authorized.ShouldBeFalse();
        result.FailureReason.ShouldBe("Outside start-node path.");
    }

    [Fact]
    public async Task AuthorizeAsync_MediaTrigger_RoutesToMediaAuthorizer()
    {
        // Media path uses AuthorizeMediaAsync — verb permissions don't apply.
        var trigger = new MediaSavedTrigger(_triggerInfra);
        var output = new MediaSavedTriggerOutput { MediaKey = Guid.NewGuid(), MediaName = "Image.png" };
        var user = Mock.Of<IUser>();

        _nodeAuthorizer
            .Setup(a => a.AuthorizeMediaAsync(user, output.MediaKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        var result = await _sut.AuthorizeAsync(
            new TriggerDispatchAuthorizationContext
            {
                Trigger = trigger,
                TypedOutput = output,
                ServiceAccount = user,
                Automation = new AutomationBuilder().WithTrigger("umbracoAutomate.mediaSaved").Build(),
            },
            CancellationToken.None);

        result.Authorized.ShouldBeTrue();
        _nodeAuthorizer.Verify(a => a.AuthorizeMediaAsync(user, output.MediaKey, It.IsAny<CancellationToken>()), Times.Once);
        _nodeAuthorizer.Verify(a => a.AuthorizeContentAsync(It.IsAny<IUser>(), It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
