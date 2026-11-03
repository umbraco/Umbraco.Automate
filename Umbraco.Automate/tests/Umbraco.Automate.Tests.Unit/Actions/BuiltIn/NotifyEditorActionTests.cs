using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Realtime;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

[Collection(Runs.StaticServiceProviderCollection.Name)]
public class NotifyEditorActionTests
{
    private const string ContentName = "Secret Quarterly Plan";

    private readonly Mock<IContentService> _contentService = new();
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IEditorNotifier> _notifier = new();
    private readonly Mock<IAutomationActionAuthorizer> _authorizer = new();
    private readonly NotifyEditorAction _action;

    public NotifyEditorActionTests()
    {
        _authorizer
            .Setup(a => a.AuthorizeContentAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Success);

        _action = new NotifyEditorAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _contentService.Object,
            _automationService.Object,
            _notifier.Object,
            _authorizer.Object,
            Mock.Of<ILogger<NotifyEditorAction>>());
    }

    [Fact]
    public async Task ExecuteAsync_AuthorizationDenied_ReturnsFailedStatus()
    {
        var result = await ExecuteDeniedAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_AuthorizationDenied_ReturnsAuthenticationError()
    {
        var result = await ExecuteDeniedAsync();

        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
    }

    [Fact]
    public async Task ExecuteAsync_AuthorizationDenied_DoesNotNotify()
    {
        await ExecuteDeniedAsync();

        _notifier.Verify(n => n.NotifyAsync(It.IsAny<EditorNotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_AuthorizationDenied_DoesNotLogContentName()
    {
        var contentKey = SetupExistingContent();
        DenyAccess(contentKey);
        var context = CreateContext(contentKey);

        await _action.ExecuteAsync(context, CancellationToken.None);

        context.LogEntries.ShouldNotContain(e => e.Message.Contains(ContentName));
    }

    [Fact]
    public async Task ExecuteAsync_Authorized_SendsNotification()
    {
        var contentKey = SetupExistingContent();

        await _action.ExecuteAsync(CreateContext(contentKey), CancellationToken.None);

        _notifier.Verify(
            n => n.NotifyAsync(It.Is<EditorNotificationMessage>(m => m.ContentKey == contentKey), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_Authorized_ReturnsSuccess()
    {
        var contentKey = SetupExistingContent();

        var result = await _action.ExecuteAsync(CreateContext(contentKey), CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
    }

    [Fact]
    public async Task ExecuteAsync_AuthorizesWithBrowsePermission()
    {
        var contentKey = SetupExistingContent();

        await _action.ExecuteAsync(CreateContext(contentKey), CancellationToken.None);

        _authorizer.Verify(
            a => a.AuthorizeContentAsync(contentKey, It.Is<IReadOnlySet<string>>(p => p.SetEquals(_action.RequiredPermissions)), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ContentNotFound_ReturnsNotFoundOutcome()
    {
        var contentKey = Guid.NewGuid();
        _contentService.Setup(s => s.GetById(contentKey)).Returns((IContent?)null);

        var result = await _action.ExecuteAsync(CreateContext(contentKey), CancellationToken.None);

        result.Outcome.ShouldBe(NotifyEditorAction.OutcomeNotFound);
    }

    [Fact]
    public async Task ExecuteAsync_ContentNotFound_DoesNotNotify()
    {
        var contentKey = Guid.NewGuid();
        _contentService.Setup(s => s.GetById(contentKey)).Returns((IContent?)null);

        await _action.ExecuteAsync(CreateContext(contentKey), CancellationToken.None);

        _notifier.Verify(n => n.NotifyAsync(It.IsAny<EditorNotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ObsoleteConstructor_ResolvesAuthorizerAndDeniesAccess()
    {
        var contentKey = SetupExistingContent();
        DenyAccess(contentKey);
        var original = StaticServiceProvider.Instance;
        try
        {
            var services = new ServiceCollection();
            services.AddSingleton(_authorizer.Object);
            StaticServiceProvider.Instance = services.BuildServiceProvider();

#pragma warning disable CS0618 // Deliberately exercising the obsolete constructor
            var legacyAction = new NotifyEditorAction(
                new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
                _contentService.Object,
                _automationService.Object,
                _notifier.Object,
                Mock.Of<ILogger<NotifyEditorAction>>());
#pragma warning restore CS0618

            var result = await legacyAction.ExecuteAsync(CreateContext(contentKey), CancellationToken.None);

            result.ErrorCategory.ShouldBe(StepRunErrorCategory.Authentication);
        }
        finally
        {
            StaticServiceProvider.Instance = original!;
        }
    }

    [Fact]
    public void HasContentSectionRequirement()
        => _action.RequiredSections.ShouldBe([Umbraco.Cms.Core.Constants.Applications.Content]);

    private async Task<ActionResult> ExecuteDeniedAsync()
    {
        var contentKey = SetupExistingContent();
        DenyAccess(contentKey);

        return await _action.ExecuteAsync(CreateContext(contentKey), CancellationToken.None);
    }

    private Guid SetupExistingContent()
    {
        var contentKey = Guid.NewGuid();
        var content = new Mock<IContent>();
        content.SetupGet(c => c.Name).Returns(ContentName);
        _contentService.Setup(s => s.GetById(contentKey)).Returns(content.Object);
        return contentKey;
    }

    private void DenyAccess(Guid contentKey)
        => _authorizer
            .Setup(a => a.AuthorizeContentAsync(contentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationAuthorizationResult.Fail("Out of start-node path."));

    private static ActionContext CreateContext(Guid contentKey)
        => new()
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.notifyEditor",
            Settings = new NotifyEditorSettings { ContentKey = contentKey.ToString() },
        };
}
