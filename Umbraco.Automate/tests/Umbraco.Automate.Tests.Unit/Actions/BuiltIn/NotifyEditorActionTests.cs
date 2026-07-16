using Microsoft.Extensions.Logging;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Realtime;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class NotifyEditorActionTests
{
    private readonly Mock<IContentService> _contentService = new();
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly Mock<IEditorNotifier> _editorNotifier = new();
    private readonly NotifyEditorAction _action;

    public NotifyEditorActionTests()
    {
        _action = new NotifyEditorAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _contentService.Object,
            _automationService.Object,
            _editorNotifier.Object,
            Mock.Of<ILogger<NotifyEditorAction>>());
    }

    [Fact]
    public async Task ExecuteAsync_ContentFound_RecordsLogEntryWithTitleAndMessage()
    {
        var contentKey = Guid.NewGuid();
        _contentService.Setup(c => c.GetById(contentKey)).Returns(Mock.Of<IContent>());
        _automationService.Setup(s => s.GetAutomationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationBuilder().WithName("Test Automation").Build());

        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.notifyEditor",
            Settings = new NotifyEditorSettings
            {
                ContentKey = contentKey.ToString(),
                Title = "Heads up",
                Message = "Something changed",
            },
            MinimumLogLevel = ActionLogLevel.Debug,
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Status.ShouldBe(ActionResultStatus.Success);
        _editorNotifier.Verify(n => n.NotifyAsync(It.IsAny<EditorNotificationMessage>(), It.IsAny<CancellationToken>()), Times.Once);

        var entry = context.LogEntries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(ActionLogLevel.Info);
        entry.Message.ShouldContain("Heads up");
        entry.Message.ShouldContain("Something changed");
    }

    [Fact]
    public async Task ExecuteAsync_ContentNotFound_DoesNotRecordLogEntry()
    {
        var contentKey = Guid.NewGuid();
        _contentService.Setup(c => c.GetById(contentKey)).Returns((IContent?)null);

        var context = new ActionContext
        {
            AutomationId = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "umbracoAutomate.notifyEditor",
            Settings = new NotifyEditorSettings { ContentKey = contentKey.ToString() },
            MinimumLogLevel = ActionLogLevel.Debug,
        };

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Outcome.ShouldBe(NotifyEditorAction.OutcomeNotFound);
        context.LogEntries.ShouldBeEmpty();
        _editorNotifier.Verify(n => n.NotifyAsync(It.IsAny<EditorNotificationMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
