using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Versioning;

namespace Umbraco.Automate.Core.Workspaces;

/// <summary>
/// Versionable entity adapter for <see cref="Workspace"/> entities.
/// </summary>
internal sealed class WorkspaceVersionableEntityAdapter : VersionableEntityAdapterBase<Workspace>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IWorkspaceService _workspaceService;
    private readonly ILogger<WorkspaceVersionableEntityAdapter> _logger;

    public WorkspaceVersionableEntityAdapter(
        IWorkspaceService workspaceService,
        ILogger<WorkspaceVersionableEntityAdapter> logger)
    {
        _workspaceService = workspaceService;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override string CreateSnapshot(Workspace entity)
        => JsonSerializer.Serialize(entity, SerializerOptions);

    /// <inheritdoc />
    protected override Workspace? RestoreFromSnapshot(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Workspace>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Workspace snapshot");
            return null;
        }
    }

    /// <inheritdoc />
    protected override IReadOnlyList<ValueChange> CompareVersions(Workspace from, Workspace to)
    {
        var changes = new List<ValueChange>();

        CompareScalar(changes, "Alias", from.Alias, to.Alias);
        CompareScalar(changes, "Name", from.Name, to.Name);
        CompareScalar(changes, "ServiceAccountKey", from.ServiceAccountKey.ToString(), to.ServiceAccountKey.ToString());

        CompareGuidList(changes, "UserGroups", from.UserGroups, to.UserGroups);
        CompareGuidList(changes, "AllowedConnections", from.AllowedConnections, to.AllowedConnections);

        return changes;
    }

    /// <inheritdoc />
    public override Task RollbackAsync(Guid entityId, int version, CancellationToken cancellationToken = default)
        => _workspaceService.RollbackWorkspaceAsync(entityId, version, cancellationToken: cancellationToken);

    /// <inheritdoc />
    protected override async Task<Workspace?> GetEntityAsync(Guid entityId, CancellationToken cancellationToken)
        => await _workspaceService.GetWorkspaceAsync(entityId, cancellationToken);

    /// <inheritdoc />
    public override Task<IReadOnlyCollection<ProtectedVersion>> GetProtectedVersionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<ProtectedVersion>>([]);

    private static void CompareScalar(List<ValueChange> changes, string path, string? oldValue, string? newValue)
    {
        if (oldValue != newValue)
        {
            changes.Add(new ValueChange(path, oldValue, newValue));
        }
    }

    private static void CompareGuidList(List<ValueChange> changes, string path, IList<Guid> from, IList<Guid> to)
    {
        var fromSet = from.ToHashSet();
        var toSet = to.ToHashSet();

        foreach (var added in toSet.Except(fromSet))
        {
            changes.Add(new ValueChange(path, null, added.ToString()));
        }

        foreach (var removed in fromSet.Except(toSet))
        {
            changes.Add(new ValueChange(path, removed.ToString(), null));
        }
    }
}
