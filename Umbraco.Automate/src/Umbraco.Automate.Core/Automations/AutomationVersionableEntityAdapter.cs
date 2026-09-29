using System.Text.Json;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Versioning;
using static Umbraco.Automate.Core.Versioning.VersionComparer;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Versionable entity adapter for <see cref="Automation"/> entities.
/// Sensitive trigger, step and notification channel settings are encrypted via
/// <see cref="IAutomationSettingsProtector"/> before being persisted into the version snapshot,
/// mirroring the at-rest encryption applied to the automation definition.
/// </summary>
internal sealed class AutomationVersionableEntityAdapter : VersionableEntityAdapterBase<Automation>
{
    /// <summary>
    /// The options every automation snapshot is written with. Shared with the job that re-encrypts
    /// snapshots stored before sensitive settings were protected, so both read the same shape.
    /// </summary>
    internal static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly IAutomationService _automationService;
    private readonly IAutomationSettingsProtector _settingsProtector;
    private readonly ILogger<AutomationVersionableEntityAdapter> _logger;

    public AutomationVersionableEntityAdapter(
        IAutomationService automationService,
        IAutomationSettingsProtector settingsProtector,
        ILogger<AutomationVersionableEntityAdapter> logger)
    {
        _automationService = automationService;
        _settingsProtector = settingsProtector;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override string CreateSnapshot(Automation entity)
    {
        // The entity is the caller's live instance (often the one just saved, still holding the
        // plaintext the user typed), so encrypt a copy rather than the original.
        var json = JsonSerializer.Serialize(entity, SerializerOptions);
        var copy = JsonSerializer.Deserialize<Automation>(json, SerializerOptions);
        if (copy is null)
        {
            return json;
        }

        _settingsProtector.ProtectAutomationSettings(copy);
        return JsonSerializer.Serialize(copy, SerializerOptions);
    }

    /// <inheritdoc />
    protected override Automation? RestoreFromSnapshot(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            var automation = JsonSerializer.Deserialize<Automation>(json, SerializerOptions);
            if (automation is not null)
            {
                _settingsProtector.UnprotectAutomationSettings(automation);
            }

            return automation;
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

    // Snapshots are decrypted when restored, so every settings comparison below masks the
    // sensitive keys. Otherwise the compare/rollback view would show credentials in plaintext.
    private void CompareTrigger(List<ValueChange> changes, TriggerConfiguration? from, TriggerConfiguration? to)
    {
        CompareScalar(changes, "Trigger.TriggerAlias", from?.TriggerAlias, to?.TriggerAlias);

        var sensitiveKeys = GetSensitiveKeys(SensitiveSettingsOwner.Trigger, from?.TriggerAlias, to?.TriggerAlias);
        var fromSettings = from?.Settings;
        var toSettings = to?.Settings;

        // A webhook trigger nests its authenticator's settings, whose sensitive fields come from
        // the selected strategy's schema rather than the trigger's. Compare that part on its own.
        if (from?.TriggerAlias == WebhookTrigger.WellKnownAlias || to?.TriggerAlias == WebhookTrigger.WellKnownAlias)
        {
            CompareWebhookAuthenticator(changes, GetValue(fromSettings, "authenticator"), GetValue(toSettings, "authenticator"));
            fromSettings = Without(fromSettings, "authenticator");
            toSettings = Without(toSettings, "authenticator");
        }

        CompareObjectDictionary(changes, "Trigger.Settings", fromSettings, toSettings, sensitiveKeys);
    }

    private void CompareWebhookAuthenticator(List<ValueChange> changes, object? from, object? to)
    {
        const string prefix = "Trigger.Settings.authenticator";

        var fromAuth = SettingsDictionary.From(from);
        var toAuth = SettingsDictionary.From(to);

        var fromAlias = Stringify(GetValue(fromAuth, "alias"));
        var toAlias = Stringify(GetValue(toAuth, "alias"));
        CompareScalar(changes, $"{prefix}.alias", fromAlias, toAlias);

        CompareObjectDictionary(
            changes,
            $"{prefix}.settings",
            SettingsDictionary.From(GetValue(fromAuth, "settings")),
            SettingsDictionary.From(GetValue(toAuth, "settings")),
            GetSensitiveKeys(SensitiveSettingsOwner.WebhookAuthenticator, fromAlias, toAlias));

        // Anything else on the authenticator object is not per-strategy settings, so compare it as-is.
        CompareObjectDictionary(changes, prefix, Without(fromAuth, "alias", "settings"), Without(toAuth, "alias", "settings"));
    }

    private void CompareSteps(List<ValueChange> changes, IList<StepConfiguration> fromSteps, IList<StepConfiguration> toSteps)
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
            CompareObjectDictionary(
                changes,
                $"{prefix}.Settings",
                fromStep.Settings,
                toStep.Settings,
                GetSensitiveKeys(SensitiveSettingsOwner.Action, fromStep.ActionAlias, toStep.ActionAlias));
            CompareStringDictionary(changes, $"{prefix}.InputMappings", fromStep.InputMappings, toStep.InputMappings);
        }
    }

    private static void CompareConnections(List<ValueChange> changes, IList<StepConnection> fromConns, IList<StepConnection> toConns)
    {
        static string Key(StepConnection c) => $"{c.SourceStepId}:{c.SourceHandle}->{c.TargetStepId}:{c.TargetHandle}";

        CompareStringSets(changes, "Connections", fromConns.Select(Key), toConns.Select(Key));
    }

    private void CompareNotificationSettings(
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
            CompareObjectDictionary(
                changes,
                $"{prefix}.Settings",
                fromChannels[i].Settings,
                toChannels[i].Settings,
                GetSensitiveKeys(SensitiveSettingsOwner.NotificationChannel, fromChannels[i].ChannelAlias, toChannels[i].ChannelAlias));
        }
    }

    /// <summary>
    /// Gets the sensitive keys of both sides of a comparison. The owner's alias can change between
    /// versions, and a key that is sensitive on either side must stay masked.
    /// </summary>
    private IReadOnlySet<string> GetSensitiveKeys(SensitiveSettingsOwner owner, string? fromAlias, string? toAlias)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in new[] { fromAlias, toAlias }.Distinct())
        {
            if (!string.IsNullOrEmpty(alias))
            {
                keys.UnionWith(_settingsProtector.GetSensitiveKeys(owner, alias));
            }
        }

        return keys;
    }

    private static object? GetValue(IDictionary<string, object?>? settings, string key)
        => settings is not null && settings.TryGetValue(key, out var value) ? value : null;

    private static Dictionary<string, object?>? Without(IDictionary<string, object?>? settings, params string[] keys)
        => settings?
            .Where(kvp => !keys.Contains(kvp.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

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
