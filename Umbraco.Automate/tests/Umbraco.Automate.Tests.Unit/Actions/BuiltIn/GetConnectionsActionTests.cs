using Json.Schema;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class GetConnectionsActionTests
{
    private readonly Mock<IConnectionService> _connectionService = new();
    private readonly GetConnectionsAction _action;

    private readonly Connection _skodaB = CreateConnection("Skoda - Vehicle B", "skoda");
    private readonly Connection _skodaA = CreateConnection("Skoda - Vehicle A", "skoda");
    private readonly Connection _github = CreateConnection("GitHub", "github");
    private readonly Connection _otherWorkspaceSkoda = CreateConnection("Skoda - Other workspace", "skoda");

    public GetConnectionsActionTests()
    {
        _connectionService.Setup(s => s.GetAllConnectionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([_skodaB, _github, _otherWorkspaceSkoda, _skodaA]);

        _action = new GetConnectionsAction(
            new ActionInfrastructure(Mock.Of<IEditableModelResolver>()),
            _connectionService.Object);
    }

    [Fact]
    public void HasCorrectAlias()
        => _action.Alias.ShouldBe("umbracoAutomate.getConnections");

    [Fact]
    public void HasCorrectName()
        => _action.Name.ShouldBe("Get Connections");

    [Fact]
    public void HasSettingsType()
        => _action.SettingsType.ShouldBe(typeof(GetConnectionsSettings));

    [Fact]
    public void HasNoConnectionTypeOfItsOwn()
        // It lists connections; it doesn't run against one, so it must not trigger connection auto-resolve.
        => _action.ConnectionTypeAlias.ShouldBeNull();

    // Literal on purpose: these keys are what saved automations store.
    [Fact]
    public void GetOutcomes_KeysAreSuccessThenNotFound_WithSuccessAsDefault()
    {
        var outcomes = _action.GetOutcomes();

        outcomes.Select(o => o.Key).ShouldBe(["success", "notFound"]);
        outcomes.Single(o => o.IsDefault).Key.ShouldBe("success");
    }

    [Fact]
    public void OutputSchema_ExposesOnlyNonSensitiveConnectionMetadata()
    {
        var schema = _action.GetOutputSchema();
        schema.ShouldNotBeNull();
        var properties = schema.GetKeyword<PropertiesKeyword>()?.Properties;
        properties.ShouldNotBeNull();
        properties.Keys.ShouldBe(["connections", "count", "first"], ignoreOrder: true);

        typeof(GetConnectionsItem).GetProperties().Select(p => p.Name)
            .ShouldBe(["Id", "Name", "Alias", "TypeAlias"], ignoreOrder: true);
    }

    [Fact]
    public async Task ExecuteAsync_NoTypeFilter_ReturnsAllAllowedConnectionsOrderedByName()
    {
        var output = await ExecuteAsync(new GetConnectionsSettings(), _skodaA.Id, _skodaB.Id, _github.Id);

        output.Connections.Select(c => c.Name).ShouldBe(["GitHub", "Skoda - Vehicle A", "Skoda - Vehicle B"]);
        output.Count.ShouldBe(3);
        output.First!.Name.ShouldBe("GitHub");
    }

    [Fact]
    public async Task ExecuteAsync_TypeFilter_ReturnsOnlyConnectionsOfThatType()
    {
        var output = await ExecuteAsync(
            new GetConnectionsSettings { ConnectionType = "skoda" }, _skodaA.Id, _skodaB.Id, _github.Id);

        output.Connections.Select(c => c.Id).ShouldBe([_skodaA.Id, _skodaB.Id]);
    }

    [Fact]
    public async Task ExecuteAsync_TypeFilter_IsCaseInsensitiveAndTrimmed()
    {
        var output = await ExecuteAsync(
            new GetConnectionsSettings { ConnectionType = " SKODA " }, _skodaA.Id, _github.Id);

        output.Connections.ShouldHaveSingleItem().Id.ShouldBe(_skodaA.Id);
    }

    [Fact]
    public async Task ExecuteAsync_NeverReturnsConnectionsOutsideTheWorkspace()
    {
        var output = await ExecuteAsync(new GetConnectionsSettings { ConnectionType = "skoda" }, _skodaA.Id);

        output.Connections.ShouldNotContain(c => c.Id == _otherWorkspaceSkoda.Id);
        output.Connections.ShouldHaveSingleItem().Id.ShouldBe(_skodaA.Id);
    }

    [Fact]
    public async Task ExecuteAsync_MapsConnectionMetadata()
    {
        var output = await ExecuteAsync(new GetConnectionsSettings(), _github.Id);

        var item = output.Connections.ShouldHaveSingleItem();
        item.Id.ShouldBe(_github.Id);
        item.Name.ShouldBe("GitHub");
        item.Alias.ShouldBe(_github.Alias);
        item.TypeAlias.ShouldBe("github");
    }

    [Fact]
    public async Task ExecuteAsync_Matches_UsesTheDefaultOutcome()
    {
        var result = await ExecuteRawAsync(new GetConnectionsSettings(), _github.Id);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_NoMatches_ReturnsNotFoundWithAnEmptyList()
    {
        var result = await ExecuteRawAsync(new GetConnectionsSettings { ConnectionType = "slack" }, _skodaA.Id, _github.Id);

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.Outcome.ShouldBe(GetConnectionsAction.OutcomeNotFound);
        var output = result.OutputData.ShouldBeOfType<GetConnectionsOutput>();
        output.Connections.ShouldBeEmpty();
        output.First.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WorkspaceWithoutConnections_ReturnsNotFoundWithoutQueryingConnections()
    {
        var result = await ExecuteRawAsync(new GetConnectionsSettings());

        result.Outcome.ShouldBe(GetConnectionsAction.OutcomeNotFound);
        _connectionService.Verify(s => s.GetAllConnectionsAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAnExecutionContext_ReturnsNotFound()
    {
        var context = CreateContext(new GetConnectionsSettings(), executionContext: null);

        var result = await _action.ExecuteAsync(context, CancellationToken.None);

        result.Outcome.ShouldBe(GetConnectionsAction.OutcomeNotFound);
    }

    private async Task<GetConnectionsOutput> ExecuteAsync(GetConnectionsSettings settings, params Guid[] allowedConnections)
        => (await ExecuteRawAsync(settings, allowedConnections)).OutputData.ShouldBeOfType<GetConnectionsOutput>();

    private Task<ActionResult> ExecuteRawAsync(GetConnectionsSettings settings, params Guid[] allowedConnections)
        => _action.ExecuteAsync(CreateContext(settings, CreateExecutionContext(allowedConnections)), CancellationToken.None);

    private static ActionContext CreateContext(GetConnectionsSettings settings, AutomationExecutionContext? executionContext) => new()
    {
        AutomationId = Guid.NewGuid(),
        RunId = Guid.NewGuid(),
        StepId = Guid.NewGuid(),
        ActionAlias = "umbracoAutomate.getConnections",
        Settings = settings,
        ExecutionContext = executionContext,
    };

    private static AutomationExecutionContext CreateExecutionContext(IReadOnlyList<Guid> allowedConnections) => new()
    {
        ServiceAccountKey = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        WorkspaceName = "Workspace",
        AutomationId = Guid.NewGuid(),
        AutomationName = "Automation",
        RunId = Guid.NewGuid(),
        InitiatorType = "Manual",
        AllowedConnections = allowedConnections,
    };

    private static Connection CreateConnection(string name, string type) => new()
    {
        Id = Guid.NewGuid(),
        Alias = name.ToLowerInvariant().Replace(' ', '-'),
        Name = name,
        Type = type,
        Settings = new Dictionary<string, object?> { ["apiKey"] = "secret" },
    };
}
