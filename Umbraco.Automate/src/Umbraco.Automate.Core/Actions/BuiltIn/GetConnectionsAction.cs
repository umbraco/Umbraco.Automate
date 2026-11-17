using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.Core.StepTypes;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// A built-in action that lists the connections the automation's workspace may use, optionally
/// filtered by connection type. Combined with a For Each, it lets an automation run the same
/// steps once per connection, e.g. one per configured vehicle or account.
/// </summary>
/// <remarks>
/// Only connections in the workspace's allowed connections are returned, so an automation can
/// never discover connections outside its workspace. The output carries non-sensitive metadata
/// only (ID, name, alias, type), never connection settings, which may hold credentials.
/// </remarks>
[Action("umbracoAutomate.getConnections", "Get Connections",
    Description = "Lists the connections available in this workspace, optionally filtered by connection type.",
    Group = "Utilities",
    Icon = "icon-link")]
public sealed class GetConnectionsAction : ActionBase<GetConnectionsSettings, GetConnectionsOutput>
{
    /// <summary>
    /// Outcome key for the normal result. It is the default outcome, so it is also the key of
    /// the canvas handle that a connection from a successful step leaves from.
    /// </summary>
    public const string OutcomeSuccess = "success";

    /// <summary>Outcome emitted when no connection matches.</summary>
    public const string OutcomeNotFound = "notFound";

    private readonly IConnectionService _connectionService;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetConnectionsAction"/> class.
    /// </summary>
    public GetConnectionsAction(ActionInfrastructure infrastructure, IConnectionService connectionService)
        : base(infrastructure)
    {
        _connectionService = connectionService;
    }

    /// <inheritdoc />
    public override IReadOnlyList<StepOutcome> GetOutcomes()
        =>
        [
            new StepOutcome(OutcomeSuccess, "#uaOutcomes_found") { IsDefault = true },
            new StepOutcome(OutcomeNotFound, "#uaOutcomes_notFound"),
        ];

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<GetConnectionsSettings>();
        var allowed = context.ExecutionContext?.AllowedConnections.ToHashSet() ?? [];
        var typeFilter = string.IsNullOrWhiteSpace(settings.ConnectionType) ? null : settings.ConnectionType.Trim();

        var connections = allowed.Count == 0
            ? []
            : (await _connectionService.GetAllConnectionsAsync(cancellationToken))
                .Where(c => allowed.Contains(c.Id))
                .Where(c => typeFilter is null || string.Equals(c.Type, typeFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Select(c => new GetConnectionsItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    Alias = c.Alias,
                    TypeAlias = c.Type,
                })
                .ToList();

        var output = new GetConnectionsOutput { Connections = connections };

        return connections.Count == 0
            ? SuccessWithOutcome(OutcomeNotFound, output)
            : Success(output);
    }
}
