using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Cms.Core.Events;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Resets an automation's circuit-breaker health when it is (re-)published — the assumption being
/// the owner has fixed the misconfiguration. Failures are isolated.
/// </summary>
internal sealed class CircuitBreakerResetHandler
    : INotificationAsyncHandler<AutomationPublishedNotification>
{
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly ILogger<CircuitBreakerResetHandler> _logger;

    public CircuitBreakerResetHandler(
        ICircuitBreakerService circuitBreaker,
        ILogger<CircuitBreakerResetHandler> logger)
    {
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    public async Task HandleAsync(AutomationPublishedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await _circuitBreaker.ResetAsync(notification.Automation.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Circuit breaker reset failed for automation {AutomationId}",
                notification.Automation.Id);
        }
    }
}
