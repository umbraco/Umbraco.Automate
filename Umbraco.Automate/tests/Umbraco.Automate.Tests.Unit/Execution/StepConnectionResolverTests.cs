using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

/// <summary>
/// How <see cref="StepConnectionResolver"/> picks the connection a step runs with: an explicit
/// connection ID, a connection binding checked at run time, or auto-resolve by connection type.
/// </summary>
public class StepConnectionResolverTests
{
    private const string SkodaType = "skoda";

    private readonly Mock<IConnectionService> _connectionService = new();
    private readonly StepConnectionResolver _resolver;

    public StepConnectionResolverTests()
    {
        var evaluator = new BindingEvaluator(new BindingFilterCollection(Array.Empty<IBindingFilter>));
        _resolver = new StepConnectionResolver(_connectionService.Object, evaluator, NullLogger.Instance);
    }

    // --- Explicit connection ID ---

    [Fact]
    public async Task ExplicitConnectionId_ReturnsThatConnection()
    {
        var connection = CreateConnection(SkodaType);
        _connectionService.Setup(s => s.GetConfiguredConnectionAsync(connection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var result = await ResolveAsync(new StepConfiguration { Id = Guid.NewGuid(), ActionAlias = "a", Name = "n", ConnectionId = connection.Id });

        result.ShouldBeSameAs(connection);
    }

    // --- Connection binding ---

    [Fact]
    public async Task Binding_ResolvingToAnAllowedConnectionOfTheRightType_ReturnsThatConnection()
    {
        var connection = CreateConnection(SkodaType);
        _connectionService.Setup(s => s.GetConfiguredConnectionAsync(connection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var result = await ResolveAsync(
            BoundStep("${ loop.item.id }"),
            allowedConnections: [connection.Id],
            bindingData: LoopItem(connection.Id.ToString()));

        result.ShouldBeSameAs(connection);
    }

    [Fact]
    public async Task Binding_WinsOverAutoResolve()
    {
        var bound = CreateConnection(SkodaType);
        var other = CreateConnection(SkodaType);
        _connectionService.Setup(s => s.GetConfiguredConnectionAsync(bound.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(bound);
        _connectionService.Setup(s => s.GetConfiguredConnectionsByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([other, bound]);

        var result = await ResolveAsync(
            BoundStep("${ loop.item.id }"),
            allowedConnections: [other.Id, bound.Id],
            bindingData: LoopItem(bound.Id.ToString()));

        result.ShouldBeSameAs(bound);
    }

    [Fact]
    public async Task Binding_ResolvingToNoValue_Throws()
    {
        var ex = await Should.ThrowAsync<ConnectionBindingException>(() => ResolveAsync(
            BoundStep("${ loop.item.missing }"),
            bindingData: LoopItem(Guid.NewGuid().ToString())));

        ex.Message.ShouldContain("${ loop.item.missing }");
        ex.Message.ShouldContain("resolved to no value");
    }

    [Fact]
    public async Task Binding_ResolvingToSomethingOtherThanAGuid_Throws()
    {
        var ex = await Should.ThrowAsync<ConnectionBindingException>(() => ResolveAsync(
            BoundStep("${ loop.item.id }"),
            bindingData: LoopItem("vehicle-a")));

        ex.Message.ShouldContain("'vehicle-a'");
        ex.Message.ShouldContain("Expected a connection ID");
    }

    [Fact]
    public async Task Binding_ResolvingToAConnectionOutsideTheWorkspace_ThrowsWithoutLookingItUp()
    {
        var connectionId = Guid.NewGuid();

        var ex = await Should.ThrowAsync<ConnectionBindingException>(() => ResolveAsync(
            BoundStep("${ loop.item.id }"),
            allowedConnections: [Guid.NewGuid()],
            bindingData: LoopItem(connectionId.ToString())));

        ex.Message.ShouldContain(connectionId.ToString());
        ex.Message.ShouldContain("not allowed in this automation's workspace");
        _connectionService.Verify(
            s => s.GetConfiguredConnectionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Binding_WithoutAnExecutionContext_Throws()
    {
        var connectionId = Guid.NewGuid();

        await Should.ThrowAsync<ConnectionBindingException>(() => _resolver.ResolveConnectionAsync(
            BoundStep("${ loop.item.id }"),
            SkodaType,
            executionContext: null,
            LoopItem(connectionId.ToString()),
            CancellationToken.None));
    }

    [Fact]
    public async Task Binding_ResolvingToAConnectionThatDoesNotExist_Throws()
    {
        var connectionId = Guid.NewGuid();
        _connectionService.Setup(s => s.GetConfiguredConnectionAsync(connectionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ConfiguredConnection?)null);

        var ex = await Should.ThrowAsync<ConnectionBindingException>(() => ResolveAsync(
            BoundStep("${ loop.item.id }"),
            allowedConnections: [connectionId],
            bindingData: LoopItem(connectionId.ToString())));

        ex.Message.ShouldContain("was not found");
    }

    [Fact]
    public async Task Binding_ResolvingToAConnectionOfAnotherType_Throws()
    {
        var connection = CreateConnection("github");
        _connectionService.Setup(s => s.GetConfiguredConnectionAsync(connection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var ex = await Should.ThrowAsync<ConnectionBindingException>(() => ResolveAsync(
            BoundStep("${ loop.item.id }"),
            allowedConnections: [connection.Id],
            bindingData: LoopItem(connection.Id.ToString())));

        ex.Message.ShouldContain("'github'");
        ex.Message.ShouldContain($"'{SkodaType}'");
    }

    [Fact]
    public async Task Binding_OnAnActionWithoutAConnectionType_AcceptsAnyType()
    {
        var connection = CreateConnection("github");
        _connectionService.Setup(s => s.GetConfiguredConnectionAsync(connection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var result = await ResolveAsync(
            BoundStep("${ loop.item.id }"),
            connectionTypeAlias: null,
            allowedConnections: [connection.Id],
            bindingData: LoopItem(connection.Id.ToString()));

        result.ShouldBeSameAs(connection);
    }

    [Fact]
    public async Task Binding_ResolvingToAGuidWithSurroundingWhitespace_IsAccepted()
    {
        var connection = CreateConnection(SkodaType);
        _connectionService.Setup(s => s.GetConfiguredConnectionAsync(connection.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection);

        var result = await ResolveAsync(
            BoundStep(" ${ loop.item.id } "),
            allowedConnections: [connection.Id],
            bindingData: LoopItem(connection.Id.ToString()));

        result.ShouldBeSameAs(connection);
    }

    // --- Auto-resolve ---

    [Fact]
    public async Task NoConnectionOrBinding_AutoResolvesTheFirstConnectionOfTheActionsType()
    {
        var github = CreateConnection("github");
        var skoda = CreateConnection(SkodaType);
        _connectionService.Setup(s => s.GetConfiguredConnectionsByIdsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([github, skoda]);

        var result = await ResolveAsync(
            new StepConfiguration { Id = Guid.NewGuid(), ActionAlias = "a", Name = "n" },
            allowedConnections: [github.Id, skoda.Id]);

        result.ShouldBeSameAs(skoda);
    }

    [Fact]
    public async Task NoConnectionOrBindingOnAnActionWithoutAConnectionType_ReturnsNull()
    {
        var result = await ResolveAsync(
            new StepConfiguration { Id = Guid.NewGuid(), ActionAlias = "a", Name = "n" },
            connectionTypeAlias: null);

        result.ShouldBeNull();
    }

    private Task<ConfiguredConnection?> ResolveAsync(
        StepConfiguration step,
        string? connectionTypeAlias = SkodaType,
        Guid[]? allowedConnections = null,
        Dictionary<string, object?>? bindingData = null)
        => _resolver.ResolveConnectionAsync(
            step,
            connectionTypeAlias,
            CreateExecutionContext(allowedConnections ?? []),
            bindingData ?? [],
            CancellationToken.None);

    private static StepConfiguration BoundStep(string binding) => new()
    {
        Id = Guid.NewGuid(),
        ActionAlias = "a",
        Name = "n",
        ConnectionBinding = binding,
    };

    private static Dictionary<string, object?> LoopItem(string id) => new(StringComparer.OrdinalIgnoreCase)
    {
        ["loop"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["item"] = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["id"] = id },
            ["index"] = 0,
        },
    };

    private static ConfiguredConnection CreateConnection(string type)
        => new(
            new Connection { Id = Guid.NewGuid(), Alias = $"{type}-{Guid.NewGuid():N}", Name = $"{type} connection", Type = type },
            Mock.Of<IConnectionType>(),
            resolvedSettings: null);

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
}
