using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Core.Execution;

/// <summary>
/// Default implementation of <see cref="IRateLimitService"/>.
/// Checks recent run count and concurrent running count against configured limits.
/// </summary>
internal sealed class RateLimitService : IRateLimitService
{
    private readonly IOptions<RateLimitingOptions> _options;
    private readonly IAutomationRunRepository _runRepository;

    public RateLimitService(
        IOptions<RateLimitingOptions> options,
        IAutomationRunRepository runRepository)
    {
        _options = options;
        _runRepository = runRepository;
    }

    public async Task CheckRateLimitAsync(Guid automationId, CancellationToken cancellationToken = default)
    {
        var opts = _options.Value;
        if (!opts.Enabled)
        {
            return;
        }

        // Check runs-per-minute limit.
        var since = DateTime.UtcNow.AddMinutes(-1);
        var recentCount = await _runRepository.GetRecentRunCountAsync(automationId, since, cancellationToken);

        if (recentCount >= opts.MaxRunsPerAutomationPerMinute)
        {
            throw new RateLimitExceededException(
                automationId,
                $"Automation '{automationId}' exceeded rate limit of {opts.MaxRunsPerAutomationPerMinute} runs per minute ({recentCount} recent runs).");
        }

        // Check concurrent runs limit.
        var concurrentCount = await _runRepository.GetConcurrentRunCountAsync(automationId, cancellationToken);

        if (concurrentCount >= opts.MaxConcurrentRunsPerAutomation)
        {
            throw new RateLimitExceededException(
                automationId,
                $"Automation '{automationId}' exceeded concurrent run limit of {opts.MaxConcurrentRunsPerAutomation} ({concurrentCount} running).");
        }
    }
}
