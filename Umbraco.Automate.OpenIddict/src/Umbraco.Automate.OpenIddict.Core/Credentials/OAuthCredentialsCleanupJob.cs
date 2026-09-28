using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Connections;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Runtime;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Infrastructure.HostedServices;

namespace Umbraco.Automate.OpenIddict.Credentials;

/// <summary>
/// Deletes stored OAuth credentials that no connection refers to — authentications that were started
/// but never saved to a connection, or that a connection has since replaced or been deleted with.
/// </summary>
/// <remarks>
/// Only credentials untouched for <see cref="MinimumAge"/> are considered. That is far longer than a
/// handoff token lives (<see cref="OAuthCredentialsHandoffProtector.Lifetime"/>), so a credential still
/// waiting for its connection to be saved is never deleted. Runs on the scheduling server only (Umbraco's
/// default server roles) — see <see cref="PerformExecuteAsync"/>.
/// </remarks>
internal sealed class OAuthCredentialsCleanupJob : RecurringHostedServiceBase
{
    /// <summary>
    /// How long a credential must have gone unmodified before it can be deleted.
    /// </summary>
    internal static readonly TimeSpan MinimumAge = TimeSpan.FromHours(24);

    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(5);

    private readonly IOAuthCredentialsService _credentialsService;
    private readonly IConnectionService _connectionService;
    private readonly IRuntimeState _runtimeState;
    private readonly IServerRoleAccessor _serverRoleAccessor;
    private readonly IMainDom _mainDom;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OAuthCredentialsCleanupJob> _logger;

    public OAuthCredentialsCleanupJob(
        IOAuthCredentialsService credentialsService,
        IConnectionService connectionService,
        IRuntimeState runtimeState,
        IServerRoleAccessor serverRoleAccessor,
        IMainDom mainDom,
        TimeProvider timeProvider,
        ILogger<OAuthCredentialsCleanupJob> logger)
        : base(logger, CleanupInterval, StartupDelay)
    {
        _credentialsService = credentialsService;
        _connectionService = connectionService;
        _runtimeState = runtimeState;
        _serverRoleAccessor = serverRoleAccessor;
        _mainDom = mainDom;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public override async Task PerformExecuteAsync(object? state)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        switch (_serverRoleAccessor.CurrentServerRole)
        {
            case ServerRole.Subscriber:
                _logger.LogDebug("OAuth credentials cleanup will not run on subscriber servers");
                return;
            case ServerRole.Unknown:
                _logger.LogDebug("OAuth credentials cleanup will not run on servers with unknown role");
                return;
            case ServerRole.Single:
            case ServerRole.SchedulingPublisher:
            default:
                break;
        }

        if (!_mainDom.IsMainDom)
        {
            _logger.LogDebug("OAuth credentials cleanup will not run if not MainDom");
            return;
        }

        await RunJobAsync(CancellationToken.None);
    }

    /// <summary>
    /// Runs the cleanup, logging and swallowing any failure (e.g. the database is not installed or
    /// migrated yet) so the recurring job tries again next period rather than faulting permanently.
    /// </summary>
    internal async Task RunJobAsync(CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await DeleteUnreferencedCredentialsAsync(cancellationToken);
            if (deleted > 0)
            {
                _logger.LogInformation("Deleted {Count} OAuth credential(s) not used by any connection", deleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // E.g. the database is not installed or migrated yet. Try again next period.
            _logger.LogWarning(ex, "Failed to clean up OAuth credentials not used by any connection");
        }
    }

    /// <summary>
    /// Deletes every credential older than <see cref="MinimumAge"/> that no connection refers to.
    /// </summary>
    /// <returns>The number of credentials deleted.</returns>
    internal async Task<int> DeleteUnreferencedCredentialsAsync(CancellationToken cancellationToken)
    {
        var cutoff = _timeProvider.GetUtcNow().UtcDateTime - MinimumAge;

        var stale = await _credentialsService.GetCredentialIdsNotModifiedSinceAsync(cutoff, cancellationToken);
        if (stale.Count == 0)
        {
            return 0;
        }

        // Read the connections after the candidates, so any connection saved in between is seen.
        var connections = await _connectionService.GetAllConnectionsAsync(cancellationToken);
        var deletable = OAuthCredentialReferences.SelectUnreferenced(stale, connections);

        foreach (var id in deletable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _credentialsService.DeleteCredentialsAsync(id, cancellationToken);
        }

        return deletable.Count;
    }
}
