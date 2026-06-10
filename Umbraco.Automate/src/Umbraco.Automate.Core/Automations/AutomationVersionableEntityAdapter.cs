using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Versioning;
using static Umbraco.Automate.Core.Versioning.VersionComparer;

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
    protected override IReadOnlyList<ValueChange> CompareVersions(Automation from, Automation to)
    {
        var changes = new List<ValueChange>();

        // Top-level properties
        CompareScalar(changes, "Alias", from.Alias, to.Alias);
        CompareScalar(changes, "Name", from.Name, to.Name);
        CompareScalar(changes, "Description", from.Description, to.Description);
        CompareScalar(changes, "Status", from.Status.ToString(), to.Status.ToString());
        CompareScalar(changes, "WorkspaceId", from.WorkspaceId.ToString(), to.WorkspaceId.ToString());
        CompareScalar(changes, "GroupId", from.GroupId?.ToString(), to.GroupId?.ToString());

        // Trigger
        CompareTrigger(changes, from.Trigger, to.Trigger);

        // Steps (by ID)
        CompareSteps(changes, from.Steps, to.Steps);

        // Connections
        CompareConnections(changes, from.Connections, to.Connections);

        // Notification settings
        CompareNotificationSettings(changes, from.NotificationSettings, to.NotificationSettings);

        return changes;
    }

    private static void CompareTrigger(List<ValueChange> changes, TriggerConfiguration? from, TriggerConfiguration? to)
    {
        CompareScalar(changes, "Trigger.TriggerAlias", from?.TriggerAlias, to?.TriggerAlias);
        CompareObjectDictionary(changes, "Trigger.Settings", from?.Settings, to?.Settings);
    }

    private static void CompareSteps(List<ValueChange> changes, IList<StepConfiguration> fromSteps, IList<StepConfiguration> toSteps)
    {
        var fromById = fromSteps.ToDictionary(s => s.Id);
        var toById = toSteps.ToDictionary(s => s.Id);

        // Added steps
        foreach (var step in toSteps.Where(s => !fromById.ContainsKey(s.Id)))
        {
            changes.Add(new ValueChange($"Steps[{step.Id}]", null, step.Name ?? step.ActionAlias));
        }

        // Removed steps
        foreach (var step in fromSteps.Where(s => !toById.ContainsKey(s.Id)))
        {
            changes.Add(new ValueChange($"Steps[{step.Id}]", step.Name ?? step.ActionAlias, null));
        }

        // Modified steps
        foreach (var fromStep in fromSteps)
        {
            if (!toById.TryGetValue(fromStep.Id, out var toStep))
            {
                continue;
            }

            var prefix = $"Steps[{fromStep.Id}]";
            CompareScalar(changes, $"{prefix}.ActionAlias", fromStep.ActionAlias, toStep.ActionAlias);
            CompareScalar(changes, $"{prefix}.Name", fromStep.Name, toStep.Name);
            CompareScalar(changes, $"{prefix}.ConnectionId", fromStep.ConnectionId?.ToString(), toStep.ConnectionId?.ToString());
            CompareScalar(changes, $"{prefix}.ErrorBehavior", fromStep.ErrorBehavior.ToString(), toStep.ErrorBehavior.ToString());
            CompareScalar(changes, $"{prefix}.RetryInterval", fromStep.RetryInterval?.ToString(), toStep.RetryInterval?.ToString());
            CompareScalar(changes, $"{prefix}.MaxRetries", fromStep.MaxRetries?.ToString(), toStep.MaxRetries?.ToString());
            CompareObjectDictionary(changes, $"{prefix}.Settings", fromStep.Settings, toStep.Settings);
            CompareStringDictionary(changes, $"{prefix}.InputMappings", fromStep.InputMappings, toStep.InputMappings);
        }
    }

    private static void CompareConnections(List<ValueChange> changes, IList<StepConnection> fromConns, IList<StepConnection> toConns)
    {
        static string Key(StepConnection c) => $"{c.SourceStepId}:{c.SourceHandle}->{c.TargetStepId}:{c.TargetHandle}";

        CompareStringSets(changes, "Connections", fromConns.Select(Key), toConns.Select(Key));
    }

    private static void CompareNotificationSettings(
        List<ValueChange> changes,
        AutomationNotificationSettings? from,
        AutomationNotificationSettings? to)
    {
        var fromChannels = from?.Channels ?? [];
        var toChannels = to?.Channels ?? [];

        if (fromChannels.Count != toChannels.Count)
        {
            CompareScalar(changes, "NotificationSettings.Channels.Count",
                fromChannels.Count.ToString(), toChannels.Count.ToString());
        }

        // Compare channels by index (positional — channels don't have stable IDs)
        var maxChannels = Math.Max(fromChannels.Count, toChannels.Count);
        for (var i = 0; i < maxChannels; i++)
        {
            var prefix = $"NotificationSettings.Channels[{i}]";
            if (i >= fromChannels.Count)
            {
                changes.Add(new ValueChange(prefix, null, toChannels[i].ChannelAlias));
                continue;
            }

            if (i >= toChannels.Count)
            {
                changes.Add(new ValueChange(prefix, fromChannels[i].ChannelAlias, null));
                continue;
            }

            CompareScalar(changes, $"{prefix}.ChannelAlias", fromChannels[i].ChannelAlias, toChannels[i].ChannelAlias);
            CompareScalar(changes, $"{prefix}.IsEnabled", fromChannels[i].IsEnabled.ToString(), toChannels[i].IsEnabled.ToString());
            CompareScalar(changes, $"{prefix}.NotifyOn", fromChannels[i].NotifyOn.ToString(), toChannels[i].NotifyOn.ToString());
            CompareObjectDictionary(changes, $"{prefix}.Settings", fromChannels[i].Settings, toChannels[i].Settings);
        }
    }

    /// <inheritdoc />
    public override Task RollbackAsync(Guid entityId, int version, Guid? userId = null, CancellationToken cancellationToken = default)
        => _automationService.RollbackAutomationAsync(entityId, version, userId, cancellationToken);

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
