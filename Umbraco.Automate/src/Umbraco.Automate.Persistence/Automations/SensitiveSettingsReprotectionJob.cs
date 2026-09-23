using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Automations;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Umbraco.Automate.Persistence.Automations;

/// <summary>
/// One-off repair that encrypts sensitive automation settings stored in plaintext by earlier
/// versions: notification channel secrets in the automation definition, and trigger, step and
/// channel secrets in automation version snapshots.
/// </summary>
/// <remarks>
/// <para>
/// This is a data repair rather than a schema change, and it needs the data protection keys and the
/// registered trigger, action and channel schemas, so it cannot be an EF Core migration. It is not a
/// package migration plan step either, because <c>PackageMigrationsUnattended: false</c> would leave
/// it pending until someone runs it by hand, and Automate's data may live in a different database
/// from the plan state.
/// </para>
/// <para>
/// It runs once, on the scheduling server only, and records completion in the key/value store. It is
/// safe to run again: encrypting is idempotent and stored values are never decrypted, so a row is
/// only written when it still held a plaintext secret. A failed run is retried on the next period.
/// </para>
/// </remarks>
internal sealed class SensitiveSettingsReprotectionJob : RecurringHostedServiceBase
{
    /// <summary>
    /// The key/value store key recording that the repair has completed.
    /// </summary>
    internal const string CompletedKey = "Umbraco.Automate.SensitiveSettingsReprotected";

    private const int BatchSize = 100;

    private static readonly TimeSpan Period = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(1);

    private readonly IDbContextFactory<UmbracoAutomateDbContext> _dbContextFactory;
    private readonly AutomationFactory _automationFactory;
    private readonly IAutomationSettingsProtector _settingsProtector;
    private readonly IKeyValueService _keyValueService;
    private readonly AutomateReadinessSignal _readinessSignal;
    private readonly IRuntimeState _runtimeState;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IMainDom _mainDom;
    private readonly ILogger<SensitiveSettingsReprotectionJob> _logger;

    private bool _completed;

    public SensitiveSettingsReprotectionJob(
        IDbContextFactory<UmbracoAutomateDbContext> dbContextFactory,
        AutomationFactory automationFactory,
        IAutomationSettingsProtector settingsProtector,
        IKeyValueService keyValueService,
        AutomateReadinessSignal readinessSignal,
        IRuntimeState runtimeState,
        IServerRoleAccessor serverRoleAccessor,
        IMainDom mainDom,
        ILogger<SensitiveSettingsReprotectionJob> logger)
        : base(logger, Period, StartupDelay, TimeProvider.System)
    {
        _dbContextFactory = dbContextFactory;
        _automationFactory = automationFactory;
        _settingsProtector = settingsProtector;
        _keyValueService = keyValueService;
        _readinessSignal = readinessSignal;
        _runtimeState = runtimeState;
        _serverRoleAccessor = serverRoleAccessor;
        _mainDom = mainDom;
        _logger = logger;
    }

    public override async Task PerformExecuteAsync(CancellationToken cancellationToken)
    {
        if (_completed || _runtimeState.Level != RuntimeLevel.Run || !_readinessSignal.IsReady)
        {
            return;
        }

        if (_serverRoleAccessor.CurrentServerRole is not (ServerRole.Single or ServerRole.SchedulingPublisher))
        {
            return;
        }

        if (!_mainDom.IsMainDom)
        {
            return;
        }

        if (_keyValueService.GetValue(CompletedKey) is not null)
        {
            _completed = true;
            return;
        }

        try
        {
            var definitions = await ReprotectDefinitionsAsync(cancellationToken);
            var snapshots = await ReprotectSnapshotsAsync(cancellationToken);

            _keyValueService.SetValue(CompletedKey, DateTime.UtcNow.ToString("O"));
            _completed = true;

            if (definitions > 0 || snapshots > 0)
            {
                _logger.LogInformation(
                    "Encrypted sensitive settings stored in plaintext in {Definitions} automation definitions and {Snapshots} automation version snapshots",
                    definitions,
                    snapshots);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown requested mid-repair. Rows already written stay encrypted; the rest are picked up next start.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt sensitive automation settings stored in plaintext. Retrying on the next run");
        }
    }

    private async Task<int> ReprotectDefinitionsAsync(CancellationToken cancellationToken)
    {
        List<Guid> ids;
        await using (var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ids = await db.Automations
                .Where(a => a.Definition != null)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);
        }

        var updated = 0;
        foreach (var id in ids)
        {
            // One context per automation, so a concurrency conflict on one row cannot poison the rest.
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var entity = await db.Automations.FindAsync([id], cancellationToken);
            if (entity is null || !_automationFactory.ReprotectDefinition(entity))
            {
                continue;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                updated++;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Saved by a user since we read it. That save went through AutomationFactory, which
                // encrypts every sensitive value, so there is nothing left to repair.
            }
        }

        return updated;
    }

    private async Task<int> ReprotectSnapshotsAsync(CancellationToken cancellationToken)
    {
        const string entityType = nameof(Automation);

        List<Guid> ids;
        await using (var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            ids = await db.EntityVersions
                .Where(v => v.EntityType == entityType)
                .Select(v => v.Id)
                .ToListAsync(cancellationToken);
        }

        var updated = 0;
        foreach (var chunk in ids.Chunk(BatchSize))
        {
            await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);

            var batch = await db.EntityVersions
                .Where(v => chunk.Contains(v.Id))
                .ToListAsync(cancellationToken);

            foreach (var version in batch)
            {
                var reprotected = ReprotectSnapshot(version.Snapshot);
                if (reprotected is null || reprotected == version.Snapshot)
                {
                    continue;
                }

                version.Snapshot = reprotected;
                updated++;
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }

    /// <summary>
    /// Encrypts any plaintext sensitive value in a snapshot without decrypting the rest, for the
    /// same reason as <see cref="AutomationFactory.ReprotectDefinition"/>.
    /// </summary>
    private string? ReprotectSnapshot(string snapshot)
    {
        Automation? automation;
        try
        {
            automation = JsonSerializer.Deserialize<Automation>(snapshot, AutomationVersionableEntityAdapter.SerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Skipping an automation version snapshot that could not be read");
            return null;
        }

        if (automation is null)
        {
            return null;
        }

        _settingsProtector.ProtectAutomationSettings(automation);
        return JsonSerializer.Serialize(automation, AutomationVersionableEntityAdapter.SerializerOptions);
    }
}
