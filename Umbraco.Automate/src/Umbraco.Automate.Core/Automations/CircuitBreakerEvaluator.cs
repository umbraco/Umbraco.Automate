using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Notifications;
using Umbraco.Cms.Core.Events;

namespace Umbraco.Automate.Core.Automations;

/// <summary>
/// Evaluates the circuit breaker after every terminal run. A second handler on
/// <see cref="AutomationRunCompletedNotification"/> alongside the channel dispatcher; failures are
/// isolated so a breaker fault never disrupts run-completed notifications (or other handlers).
/// </summary>
internal sealed class CircuitBreakerEvaluator
    : INotificationAsyncHandler<AutomationRunCompletedNotification>
{
    private readonly ICircuitBreakerService _circuitBreaker;
    private readonly ILogger<CircuitBreakerEvaluator> _logger;

    public CircuitBreakerEvaluator(
        ICircuitBreakerService circuitBreaker,
        ILogger<CircuitBreakerEvaluator> logger)
    {
        _circuitBreaker = circuitBreaker;
        _logger = logger;
    }

    public async Task HandleAsync(AutomationRunCompletedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await _circuitBreaker.EvaluateAsync(notification.Run, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Circuit breaker evaluation failed for run {RunId} (automation {AutomationId})",
                notification.Run.Id, notification.Run.AutomationId);
        }
    }
}
