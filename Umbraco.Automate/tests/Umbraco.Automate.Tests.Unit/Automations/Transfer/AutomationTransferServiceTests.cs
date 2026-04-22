using System.Data;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Automations.Transfer;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.StepTypes;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Core.Workspaces;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Scoping;

namespace Umbraco.Automate.Tests.Unit.Automations.Transfer;

public class AutomationTransferTests
{
    private readonly Mock<IAutomationRepository> _repo = new();
    private readonly Mock<IAutomationRunRepository> _runRepo = new();
    private readonly Mock<IConnectionService> _connectionService = new();
    private readonly Mock<IWorkspaceService> _workspaceService = new();
    private readonly Mock<ICoreScopeProvider> _scopeProvider = new();
    private readonly Mock<ICoreScope> _scope = new();
    private readonly Mock<IScopedNotificationPublisher> _notifications = new();
    private readonly ActionCollection _actions;
    private readonly TriggerCollection _triggers;
    private readonly ControlFlowCollection _controlFlows;
    private readonly AutomationService _service;

    public AutomationTransferTests()
    {
        _scope.Setup(s => s.Notifications).Returns(_notifications.Object);
        _scopeProvider.Setup(p => p.CreateCoreScope(
                It.IsAny<IsolationLevel>(),
                It.IsAny<RepositoryCacheMode>(),
                It.IsAny<IEventDispatcher?>(),
                It.IsAny<IScopedNotificationPublisher?>(),
                It.IsAny<bool?>(),
                It.IsAny<bool>(),
                It.IsAny<bool>()))
            .Returns(_scope.Object);

        _actions = new ActionCollection(() => []);
        _triggers = new TriggerCollection(() => []);
        _controlFlows = new ControlFlowCollection(() => []);

        _service = new AutomationService(
            _repo.Object,
            _runRepo.Object,
            Mock.Of<IEntityVersionService>(),
            _workspaceService.Object,
            _connectionService.Object,
            _scopeProvider.Object,
            Mock.Of<IEventMessagesFactory>(),
            _actions,
            _triggers,
            _controlFlows,
            new SensitiveSettingsStripper(_actions, _triggers, _controlFlows));
    }

    #region Export Tests

    [Fact]
    public async Task ExportAutomationAsync_ReturnsNull_WhenAutomationNotFound()
    {
        _repo.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);

        var result = await _service.ExportAutomationAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExportAutomationAsync_ReturnsExportModel_WithCorrectMetadata()
    {
        var automation = new AutomationBuilder()
            .WithAlias("test-automation")
            .WithName("Test Automation")
            .WithDescription("A test automation")
            .WithManualTrigger()
            .Build();

        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _service.ExportAutomationAsync(automation.Id);

        result.ShouldNotBeNull();
        result.FormatVersion.ShouldBe("1.0");
        result.ExportedFrom.Product.ShouldBe("Umbraco.Automate");
        result.Automation.Alias.ShouldBe("test-automation");
        result.Automation.Name.ShouldBe("Test Automation");
        result.Automation.Description.ShouldBe("A test automation");
    }

    [Fact]
    public async Task ExportAutomationAsync_ExcludesNotifications_ByDefault()
    {
        var automation = new AutomationBuilder()
            .WithManualTrigger()
            .Build();

        automation.NotificationSettings = new AutomationNotificationSettings
        {
            Channels = [new ChannelConfiguration { ChannelAlias = "email", IsEnabled = true, NotifyOn = NotifyOn.Failed }],
        };

        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _service.ExportAutomationAsync(automation.Id);

        result.ShouldNotBeNull();
        result.Automation.NotificationSettings.ShouldBeNull();
    }

    [Fact]
    public async Task ExportAutomationAsync_IncludesNotifications_WhenOptedIn()
    {
        var automation = new AutomationBuilder()
            .WithManualTrigger()
            .Build();

        automation.NotificationSettings = new AutomationNotificationSettings
        {
            Channels = [new ChannelConfiguration { ChannelAlias = "email", IsEnabled = true, NotifyOn = NotifyOn.Failed }],
        };

        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var options = new AutomationExportOptions { IncludeNotifications = true };
        var result = await _service.ExportAutomationAsync(automation.Id, options);

        result.ShouldNotBeNull();
        result.Automation.NotificationSettings.ShouldNotBeNull();
        result.Automation.NotificationSettings.Channels[0].NotifyOn.ShouldBe(NotifyOn.Failed);
    }

    [Fact]
    public async Task ExportAutomationAsync_IncludesAutomationId()
    {
        var automation = new AutomationBuilder().WithManualTrigger().Build();

        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var result = await _service.ExportAutomationAsync(automation.Id);

        result.ShouldNotBeNull();
        result.Automation.Id.ShouldBe(automation.Id);
    }

    [Fact]
    public async Task ExportAutomationAsync_ResolvesConnectionAlias_FromConnectionId()
    {
        var connectionId = Guid.NewGuid();
        var connection = new ConnectionBuilder()
            .WithId(connectionId)
            .WithAlias("slack-connection")
            .WithType("slack")
            .WithName("Slack Connection")
            .Build();

        var stepId = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithManualTrigger()
            .AddStep(new StepConfiguration
            {
                Id = stepId,
                ActionAlias = "sendSlackMessage",
                Name = "Send Slack",
                ConnectionId = connectionId,
            })
            .Build();

        _repo.Setup(r => r.GetAsync(automation.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);
        _connectionService.Setup(s => s.GetConnectionAsync(connectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var result = await _service.ExportAutomationAsync(automation.Id);

        result.ShouldNotBeNull();
        result.Automation.Steps.Count.ShouldBe(1);
        result.Automation.Steps[0].ConnectionAlias.ShouldBe("slack-connection");
        result.ConnectionReferences.Count.ShouldBe(1);
        result.ConnectionReferences[0].Alias.ShouldBe("slack-connection");
        result.ConnectionReferences[0].Type.ShouldBe("slack");
        result.ConnectionReferences[0].Name.ShouldBe("Slack Connection");
    }

    #endregion

    #region Validate Import Tests

    [Fact]
    public async Task ValidateImportAsync_FailsOnUnsupportedFormatVersion()
    {
        var exportModel = BuildValidExportModel(formatVersion: "99.0");

        var result = await _service.ValidateImportAsync(exportModel, Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("Unsupported format version"));
    }

    [Fact]
    public async Task ValidateImportAsync_FailsWhenWorkspaceNotFound()
    {
        var exportModel = BuildValidExportModel();
        var workspaceId = Guid.NewGuid();

        _workspaceService.Setup(s => s.GetWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Workspace?)null);

        var result = await _service.ValidateImportAsync(exportModel, workspaceId);

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("workspace") && e.Contains("not found"));
    }

    [Fact]
    public async Task ValidateImportAsync_FailsWhenTriggerNotRegistered()
    {
        var exportModel = BuildValidExportModel(triggerAlias: "unregisteredTrigger");
        var workspaceId = SetupValidWorkspace();

        var result = await _service.ValidateImportAsync(exportModel, workspaceId);

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("unregisteredTrigger") && e.Contains("not registered"));
    }

    [Fact]
    public async Task ValidateImportAsync_FailsWhenConnectionNotFound()
    {
        var exportModel = BuildValidExportModel();
        exportModel.ConnectionReferences.Add(new ConnectionReferenceModel
        {
            Alias = "missing-connection",
            Type = "slack",
            Name = "Missing Connection",
        });

        var workspaceId = SetupValidWorkspace();

        _connectionService.Setup(s => s.GetConnectionByAliasAsync("missing-connection", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Connection?)null);

        var result = await _service.ValidateImportAsync(exportModel, workspaceId);

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("missing-connection") && e.Contains("not found"));
    }

    [Fact]
    public async Task ValidateImportAsync_FailsWhenAliasAlreadyExists()
    {
        var exportModel = BuildValidExportModel(alias: "existing-automation");
        var workspaceId = SetupValidWorkspace();

        _repo.Setup(r => r.GetByAliasAsync("existing-automation", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationBuilder().WithAlias("existing-automation").Build());

        var result = await _service.ValidateImportAsync(exportModel, workspaceId);

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("existing-automation") && e.Contains("already exists"));
    }

    [Fact]
    public async Task ValidateImportAsync_FailsWhenConnectionNotAllowedInWorkspace()
    {
        var connectionId = Guid.NewGuid();
        var connection = new ConnectionBuilder()
            .WithId(connectionId)
            .WithAlias("slack-conn")
            .WithType("slack")
            .Build();

        var exportModel = BuildValidExportModel();
        exportModel.ConnectionReferences.Add(new ConnectionReferenceModel
        {
            Alias = "slack-conn",
            Type = "slack",
            Name = "Slack",
        });

        var workspaceId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder()
            .WithId(workspaceId)
            .WithAllowedConnections() // Empty.
            .Build();

        _workspaceService.Setup(s => s.GetWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        _connectionService.Setup(s => s.GetConnectionByAliasAsync("slack-conn", It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var result = await _service.ValidateImportAsync(exportModel, workspaceId);

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("slack-conn") && e.Contains("not allowed"));
    }

    [Fact]
    public async Task ValidateImportAsync_FailsWhenIdMissing()
    {
        var exportModel = BuildValidExportModel(id: Guid.Empty);
        var workspaceId = SetupValidWorkspace();

        var result = await _service.ValidateImportAsync(exportModel, workspaceId);

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("missing an automation ID"));
    }

    [Fact]
    public async Task ValidateImportAsync_FailsWhenIdAlreadyExists_OnCreate()
    {
        var fileId = Guid.NewGuid();
        var exportModel = BuildValidExportModel(id: fileId);
        var workspaceId = SetupValidWorkspace();

        _repo.Setup(r => r.GetAsync(fileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationBuilder().WithId(fileId).Build());

        var result = await _service.ValidateImportAsync(exportModel, workspaceId);

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains(fileId.ToString()) && e.Contains("already exists"));
    }

    [Fact]
    public async Task ValidateImportAsync_FailsWhenFileIdDoesNotMatchTarget_OnOverwrite()
    {
        var fileId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var exportModel = BuildValidExportModel(id: fileId);
        var workspaceId = SetupValidWorkspace();

        var result = await _service.ValidateImportAsync(exportModel, workspaceId, existingAutomationId: targetId);

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.Contains("does not match") && e.Contains(targetId.ToString()));
    }

    [Fact]
    public async Task ValidateImportAsync_AllowsOverwrite_WhenAliasMatchesSameAutomation()
    {
        var existingId = Guid.NewGuid();
        var exportModel = BuildValidExportModel(alias: "auto-one", id: existingId);
        var workspaceId = SetupValidWorkspace();

        _repo.Setup(r => r.GetAsync(existingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationBuilder().WithId(existingId).WithAlias("auto-one").Build());
        _repo.Setup(r => r.GetByAliasAsync("auto-one", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationBuilder().WithId(existingId).WithAlias("auto-one").Build());

        var result = await _service.ValidateImportAsync(exportModel, workspaceId, existingAutomationId: existingId);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateImportAsync_Succeeds_WithNoConnectionsAndNoTrigger()
    {
        var exportModel = BuildValidExportModel(triggerAlias: null);

        var workspaceId = SetupValidWorkspace();

        var result = await _service.ValidateImportAsync(exportModel, workspaceId);

        result.Success.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }

    #endregion

    #region Import Tests

    [Fact]
    public async Task ImportAutomationAsync_CreatesAutomation_AsDraftAndDisabled()
    {
        var exportModel = BuildValidExportModel(alias: "imported-automation", triggerAlias: null);

        var workspaceId = SetupValidWorkspace();

        Automation? captured = null;
        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<Automation, Guid?, CancellationToken>((a, _, _) => captured = a)
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var result = await _service.ImportAutomationAsync(exportModel, workspaceId);

        result.Success.ShouldBeTrue();
        result.AutomationId.ShouldNotBeNull();
        result.AutomationAlias.ShouldBe("imported-automation");

        captured.ShouldNotBeNull();
        captured!.Status.ShouldBe(AutomationStatus.Draft);
        captured.IsEnabled.ShouldBeFalse();
        captured.WorkspaceId.ShouldBe(workspaceId);
    }

    [Fact]
    public async Task ImportAutomationAsync_PreservesIdFromExport()
    {
        var fileId = Guid.NewGuid();
        var exportModel = BuildValidExportModel(alias: "with-id", triggerAlias: null, id: fileId);
        var workspaceId = SetupValidWorkspace();

        Automation? captured = null;
        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<Automation, Guid?, CancellationToken>((a, _, _) => captured = a)
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var result = await _service.ImportAutomationAsync(exportModel, workspaceId);

        result.Success.ShouldBeTrue();
        result.AutomationId.ShouldBe(fileId);
        captured.ShouldNotBeNull();
        captured!.Id.ShouldBe(fileId);
    }

    [Fact]
    public async Task ImportAutomationAsync_OverwritesExisting_PreservingWorkspaceAndLifecycle()
    {
        var existingId = Guid.NewGuid();
        var workspaceId = SetupValidWorkspace();
        var existing = new AutomationBuilder()
            .WithId(existingId)
            .WithAlias("live-auto")
            .WithName("Old Name")
            .WithWorkspaceId(workspaceId)
            .WithStatus(AutomationStatus.Published)
            .WithIsEnabled(true)
            .WithPublishedVersion(3)
            .Build();

        var exportModel = BuildValidExportModel(
            alias: "live-auto",
            triggerAlias: null,
            id: existingId,
            description: "Updated from import");

        _repo.Setup(r => r.GetAsync(existingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        Automation? captured = null;
        _repo.Setup(r => r.SaveAsync(It.IsAny<Automation>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()))
            .Callback<Automation, Guid?, CancellationToken>((a, _, _) => captured = a)
            .ReturnsAsync((Automation a, Guid? _, CancellationToken _) => a);

        var result = await _service.ImportAutomationAsync(exportModel, workspaceId, existingAutomationId: existingId);

        result.Success.ShouldBeTrue();
        result.AutomationId.ShouldBe(existingId);
        captured.ShouldNotBeNull();
        captured!.Id.ShouldBe(existingId);
        captured.WorkspaceId.ShouldBe(workspaceId);
        captured.Status.ShouldBe(AutomationStatus.Published);
        captured.IsEnabled.ShouldBeTrue();
        captured.Description.ShouldBe("Updated from import");
    }

    [Fact]
    public async Task ImportAutomationAsync_FailsValidation_ReturnsErrors()
    {
        var exportModel = BuildValidExportModel(formatVersion: "99.0");

        var result = await _service.ImportAutomationAsync(exportModel, Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.AutomationId.ShouldBeNull();
    }

    #endregion

    #region Helpers

    private static AutomationExportModel BuildValidExportModel(
        string formatVersion = "1.0",
        string alias = "test-export",
        string? triggerAlias = null,
        Guid? id = null,
        string? description = null)
    {
        return new AutomationExportModel
        {
            FormatVersion = formatVersion,
            ExportedAt = DateTime.UtcNow,
            ExportedFrom = new ExportSourceModel { Product = "Umbraco.Automate", Version = "1.0.0" },
            Automation = new AutomationExportDefinition
            {
                Id = id ?? Guid.NewGuid(),
                Alias = alias,
                Name = "Test Export",
                Description = description,
                Trigger = triggerAlias is not null
                    ? new TriggerConfiguration { TriggerAlias = triggerAlias }
                    : null,
                Steps = [],
                Connections = [],
            },
            ConnectionReferences = [],
        };
    }

    private Guid SetupValidWorkspace()
    {
        var workspaceId = Guid.NewGuid();
        var workspace = new WorkspaceBuilder().WithId(workspaceId).Build();
        _workspaceService.Setup(s => s.GetWorkspaceAsync(workspaceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspace);
        return workspaceId;
    }

    #endregion
}
