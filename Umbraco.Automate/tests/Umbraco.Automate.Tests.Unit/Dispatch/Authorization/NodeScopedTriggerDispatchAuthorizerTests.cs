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

    [Fact]
    public async Task AuthorizeAndNarrowAsync_SingleContentOutputAllowed_LeavesOutputUnchanged()
    {
        var output = new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid() };
        AllowContent(output.ContentKey);

        var result = await _sut.AuthorizeAndNarrowAsync(BuildContext(output), CancellationToken.None);

        result.NarrowedOutput.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizeAndNarrowAsync_SingleContentOutputDenied_IsDenied()
    {
        var output = new ContentSavedTriggerOutput { ContentKey = Guid.NewGuid() };
        DenyContent(output.ContentKey);

        var result = await _sut.AuthorizeAndNarrowAsync(BuildContext(output), CancellationToken.None);

        result.Result.Authorized.ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizeAndNarrowAsync_BatchAllItemsAllowed_IsAuthorized()
    {
        var (batch, _, _) = BuildMixedContentBatch();
        AllowContent(batch.Items[0].ContentKey);
        AllowContent(batch.Items[1].ContentKey);

        var result = await _sut.AuthorizeAndNarrowAsync(BuildContext(batch), CancellationToken.None);

        result.Result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizeAndNarrowAsync_BatchAllItemsAllowed_LeavesOutputUnchanged()
    {
        var (batch, _, _) = BuildMixedContentBatch();
        AllowContent(batch.Items[0].ContentKey);
        AllowContent(batch.Items[1].ContentKey);

        var result = await _sut.AuthorizeAndNarrowAsync(BuildContext(batch), CancellationToken.None);

        result.NarrowedOutput.ShouldBeNull();
    }

    [Fact]
    public async Task AuthorizeAndNarrowAsync_BatchMixedAccess_IsAuthorized()
    {
        var (batch, allowedKey, deniedKey) = BuildMixedContentBatch();
        AllowContent(allowedKey);
        DenyContent(deniedKey);

        var result = await _sut.AuthorizeAndNarrowAsync(BuildContext(batch), CancellationToken.None);

        result.Result.Authorized.ShouldBeTrue();
    }

    [Fact]
    public async Task AuthorizeAndNarrowAsync_BatchMixedAccess_KeepsOnlyAllowedItems()
    {
        var (batch, allowedKey, deniedKey) = BuildMixedContentBatch();
        AllowContent(allowedKey);
        DenyContent(deniedKey);

        var result = await _sut.AuthorizeAndNarrowAsync(BuildContext(batch), CancellationToken.None);

        result.NarrowedOutput.ShouldBeOfType<BatchTriggerOutput<ContentSavedTriggerOutput>>()
            .Items.Select(i => i.ContentKey).ShouldBe([allowedKey]);
    }

    [Fact]
    public async Task AuthorizeAndNarrowAsync_BatchMixedAccess_RecomputesCount()
    {
        var (batch, allowedKey, deniedKey) = BuildMixedContentBatch();
        AllowContent(allowedKey);
        DenyContent(deniedKey);

        var result = await _sut.AuthorizeAndNarrowAsync(BuildContext(batch), CancellationToken.None);

        result.NarrowedOutput.ShouldBeOfType<BatchTriggerOutput<ContentSavedTriggerOutput>>().Count.ShouldBe(1);
    }

    [Fact]
    public async Task AuthorizeAndNarrowAsync_BatchMixedAccess_DoesNotMutateOriginalBatch()
    {
        // The original typed output is shared across every automation subscribed to the
        // event; narrowing for one service account must not leak into another's run.
        var (batch, allowedKey, deniedKey) = BuildMixedContentBatch();
        AllowContent(allowedKey);
        DenyContent(deniedKey);

        await _sut.AuthorizeAndNarrowAsync(BuildContext(batch), CancellationToken.None);

        batch.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AuthorizeAndNarrowAsync_BatchNoItemsAllowed_IsDenied()
    {
        var (batch, _, _) = BuildMixedContentBatch();
        DenyContent(batch.Items[0].ContentKey);
        DenyContent(batch.Items[1].ContentKey);

        var result = await _sut.AuthorizeAndNarrowAsync(BuildContext(batch), CancellationToken.None);

        result.Result.Authorized.ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizeAsync_BatchNoItemsAllowed_IsDenied()
    {
        // The plain ITriggerDispatchAuthorizer contract must agree with the narrowing one.
        var (batch, _, _) = BuildMixedContentBatch();
        DenyContent(batch.Items[0].ContentKey);
        DenyContent(batch.Items[1].ContentKey);

        var result = await _sut.AuthorizeAsync(BuildContext(batch), CancellationToken.None);

        result.Authorized.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(BatchTriggerTypes))]
    public void BatchTrigger_OutputType_IsNarrowableBatch(Type triggerType)
    {
        var trigger = CreateTrigger(triggerType);

        typeof(IBatchTriggerOutput).IsAssignableFrom(trigger.OutputType).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(NodeCheckedBatchTriggerTypes))]
    public async Task AuthorizeAndNarrowAsync_NodeCheckedBatchTriggerWithDeniedItem_IsDenied(Type triggerType)
    {
        // Every content batch trigger and the media saved/trashed batch triggers carry items
        // whose single-item trigger is node-checked, so the batch must be checked too.
        DenyAllNodes();

        var result = await _sut.AuthorizeAndNarrowAsync(
            BuildContext(BuildSingleItemBatch(triggerType)), CancellationToken.None);

        result.Result.Authorized.ShouldBeFalse();
    }

    [Fact]
    public async Task AuthorizeAndNarrowAsync_MediaBatchDeleted_IsNotNodeChecked()
    {
        // Deleted media no longer exists, so CMS would always report NotFound and the
        // trigger would never fire. The single-item MediaDeletedTrigger is not node-checked
        // for the same reason; the batch form matches it.
        DenyAllNodes();

        var result = await _sut.AuthorizeAndNarrowAsync(
            BuildContext(BuildSingleItemBatch(typeof(MediaBatchDeletedTrigger))), CancellationToken.None);

        result.Result.Authorized.ShouldBeTrue();
    }

    public static TheoryData<Type> BatchTriggerTypes => new()
    {
        typeof(ContentBatchPublishedTrigger),
        typeof(ContentBatchSavedTrigger),
        typeof(ContentBatchUnpublishedTrigger),
        typeof(MediaBatchSavedTrigger),
        typeof(MediaBatchTrashedTrigger),
        typeof(MediaBatchDeletedTrigger),
    };

    public static TheoryData<Type> NodeCheckedBatchTriggerTypes => new()
    {
        typeof(ContentBatchPublishedTrigger),
        typeof(ContentBatchSavedTrigger),
        typeof(ContentBatchUnpublishedTrigger),
        typeof(MediaBatchSavedTrigger),
        typeof(MediaBatchTrashedTrigger),
    };

    private static ITrigger CreateTrigger(Type triggerType)
        => (ITrigger)Activator.CreateInstance(triggerType, new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()))!;

    /// <summary>
    /// Builds a one-item batch of the trigger's own declared output type, so the case fails
    /// if the item factory and the trigger's real output type drift apart.
    /// </summary>
    private static object BuildSingleItemBatch(Type triggerType)
    {
        var key = Guid.NewGuid();
        object item = triggerType.Name switch
        {
            nameof(ContentBatchPublishedTrigger) => new ContentPublishedTriggerOutput { ContentKey = key },
            nameof(ContentBatchSavedTrigger) => new ContentSavedTriggerOutput { ContentKey = key },
            nameof(ContentBatchUnpublishedTrigger) => new ContentUnpublishedTriggerOutput { ContentKey = key },
            nameof(MediaBatchSavedTrigger) => new MediaSavedTriggerOutput { MediaKey = key },
            nameof(MediaBatchTrashedTrigger) => new MediaTrashedTriggerOutput { MediaKey = key },
            nameof(MediaBatchDeletedTrigger) => new MediaDeletedTriggerOutput { MediaKey = key },
            _ => throw new ArgumentOutOfRangeException(nameof(triggerType), triggerType.Name, "Not a batch trigger."),
        };

        var empty = (IBatchTriggerOutput)Activator.CreateInstance(CreateTrigger(triggerType).OutputType!)!;
        return empty.WithItems([item]);
    }

    private static (BatchTriggerOutput<ContentSavedTriggerOutput> Batch, Guid AllowedKey, Guid DeniedKey) BuildMixedContentBatch()
    {
        var allowedKey = Guid.NewGuid();
        var deniedKey = Guid.NewGuid();
        var batch = new BatchTriggerOutput<ContentSavedTriggerOutput>
        {
            Items =
            [
                new ContentSavedTriggerOutput { ContentKey = allowedKey, ContentName = "Inside" },
                new ContentSavedTriggerOutput { ContentKey = deniedKey, ContentName = "Outside" },
            ],
            Count = 2,
        };

        return (batch, allowedKey, deniedKey);
    }

    private void AllowContent(Guid key)
        => _nodeAuthorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<IUser>(), key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

    private void DenyContent(Guid key)
        => _nodeAuthorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<IUser>(), key, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Outside start-node path."));

    private void DenyAllNodes()
    {
        _nodeAuthorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<IUser>(), It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Outside start-node path."));
        _nodeAuthorizer
            .Setup(a => a.AuthorizeMediaAsync(It.IsAny<IUser>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Outside start-node path."));
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
