using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Versioning;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Versionable entity adapter for <see cref="Automation"/> entities.
/// </summary>
internal sealed class AutomationVersionableEntityAdapter : VersionableEntityAdapterBase<Automation>
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IAutomationService _automationService;
    private readonly ILogger<AutomationVersionableEntityAdapter> _logger;

    public AutomationVersionableEntityAdapter(
        IAutomationService automationService,
        ILogger<AutomationVersionableEntityAdapter> logger)
    {
        _automationService = automationService;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override string CreateSnapshot(Automation entity)
        => JsonSerializer.Serialize(entity, SerializerOptions);

    /// <inheritdoc />
    protected override Automation? RestoreFromSnapshot(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Automation>(json, SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Automation snapshot");
            return null;
        }
    }

    /// <inheritdoc />
    protected override void HydrateIdentity(Automation entity, Guid entityId)
        => entity.Id = entityId;

    /// <inheritdoc />
    protected override IReadOnlyList<ValueChange> CompareVersions(Automation from, Automation to)
    {
        var changes = new List<ValueChange>();

        if (from.Alias != to.Alias)
        {
            changes.Add(new ValueChange("Alias", from.Alias, to.Alias));
        }

        if (from.Name != to.Name)
        {
            changes.Add(new ValueChange("Name", from.Name, to.Name));
        }

        if (from.Description != to.Description)
        {
            changes.Add(new ValueChange("Description", from.Description, to.Description));
        }

        if (from.IsEnabled != to.IsEnabled)
        {
            changes.Add(new ValueChange("IsEnabled", from.IsEnabled.ToString(), to.IsEnabled.ToString()));
        }

        if (from.Status != to.Status)
        {
            changes.Add(new ValueChange("Status", from.Status.ToString(), to.Status.ToString()));
        }

        if (from.Trigger?.TriggerAlias != to.Trigger?.TriggerAlias)
        {
            changes.Add(new ValueChange("Trigger.TriggerAlias", from.Trigger?.TriggerAlias, to.Trigger?.TriggerAlias));
        }

        if (from.Steps.Count != to.Steps.Count)
        {
            changes.Add(new ValueChange("Steps.Count", from.Steps.Count.ToString(), to.Steps.Count.ToString()));
        }

        if (from.Connections.Count != to.Connections.Count)
        {
            changes.Add(new ValueChange("Connections.Count", from.Connections.Count.ToString(), to.Connections.Count.ToString()));
        }

        return changes;
    }

    /// <inheritdoc />
    public override Task RollbackAsync(Guid entityId, int version, CancellationToken cancellationToken = default)
        => _automationService.RollbackAutomationAsync(entityId, version, cancellationToken: cancellationToken);

    /// <inheritdoc />
    protected override async Task<Automation?> GetEntityAsync(Guid entityId, CancellationToken cancellationToken)
        => await _automationService.GetAutomationAsync(entityId, cancellationToken);

    /// <inheritdoc />
    public override async Task<IReadOnlyCollection<ProtectedVersion>> GetProtectedVersionsAsync(CancellationToken cancellationToken = default)
    {
        var refs = await _automationService.GetPublishedVersionReferencesAsync(cancellationToken);
        return refs.Select(r => new ProtectedVersion(r.Id, r.PublishedVersion)).ToList();
    }
}
