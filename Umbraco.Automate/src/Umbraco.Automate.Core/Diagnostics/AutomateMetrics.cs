using System.Diagnostics.Metrics;

namespace Umbraco.Automate.Core.Diagnostics;

/// <summary>
/// OpenTelemetry-compatible metrics for Umbraco Automate.
/// Uses <see cref="System.Diagnostics.Metrics"/> so any OTel exporter can collect them.
/// </summary>
internal sealed class AutomateMetrics
{
    public const string MeterName = "Umbraco.Automate";

    private readonly Counter<long> _automationRunsStarted;
    private readonly Counter<long> _automationRunsCompleted;
    private readonly Counter<long> _automationRunsFailed;
    private readonly Histogram<double> _runDuration;
    private readonly Counter<long> _stepsExecuted;
    private readonly Counter<long> _stepsFailed;
    private readonly Histogram<double> _stepDuration;
    private readonly Counter<long> _triggersDispatched;
    private readonly Counter<long> _rateLimitRejections;
    private readonly Counter<long> _outboxMessagesPublished;
    private readonly Counter<long> _outboxMessagesCompleted;
    private readonly Counter<long> _outboxMessagesDeadLettered;
    private readonly Counter<long> _outboxBackpressureRejections;

    private int _outboxPending;
    private int _outboxProcessing;

    public AutomateMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        _automationRunsStarted = meter.CreateCounter<long>(
            "automate.runs.started",
            description: "Number of automation runs started");

        _automationRunsCompleted = meter.CreateCounter<long>(
            "automate.runs.completed",
            description: "Number of automation runs completed successfully");

        _automationRunsFailed = meter.CreateCounter<long>(
            "automate.runs.failed",
            description: "Number of automation runs that failed");

        _runDuration = meter.CreateHistogram<double>(
            "automate.runs.duration",
            unit: "ms",
            description: "Duration of automation runs in milliseconds");

        _stepsExecuted = meter.CreateCounter<long>(
            "automate.steps.executed",
            description: "Number of action steps executed");

        _stepsFailed = meter.CreateCounter<long>(
            "automate.steps.failed",
            description: "Number of action steps that failed");

        _stepDuration = meter.CreateHistogram<double>(
            "automate.steps.duration",
            unit: "ms",
            description: "Duration of action step execution in milliseconds");

        _triggersDispatched = meter.CreateCounter<long>(
            "automate.triggers.dispatched",
            description: "Number of trigger events dispatched to the outbox");

        _rateLimitRejections = meter.CreateCounter<long>(
            "automate.rate_limits.rejected",
            description: "Number of automation runs rejected by rate limiting");

        _outboxMessagesPublished = meter.CreateCounter<long>(
            "automate.outbox.published",
            description: "Number of messages published to the outbox");

        _outboxMessagesCompleted = meter.CreateCounter<long>(
            "automate.outbox.completed",
            description: "Number of outbox messages processed successfully");

        _outboxMessagesDeadLettered = meter.CreateCounter<long>(
            "automate.outbox.dead_lettered",
            description: "Number of outbox messages that exhausted retries");

        _outboxBackpressureRejections = meter.CreateCounter<long>(
            "automate.outbox.backpressure_rejections",
            description: "Number of publish attempts rejected due to backpressure");

        meter.CreateObservableGauge(
            "automate.outbox.depth",
            observeValues: ObserveOutboxDepth,
            description: "Current number of messages in the outbox by status");
    }

    // === Runs ===

    public void RunStarted(string automationAlias) =>
        _automationRunsStarted.Add(1, new KeyValuePair<string, object?>("automation.alias", automationAlias));

    public void RunCompleted(string automationAlias) =>
        _automationRunsCompleted.Add(1, new KeyValuePair<string, object?>("automation.alias", automationAlias));

    public void RunFailed(string automationAlias) =>
        _automationRunsFailed.Add(1, new KeyValuePair<string, object?>("automation.alias", automationAlias));

    public void RecordRunDuration(double durationMs, string automationAlias) =>
        _runDuration.Record(durationMs, new KeyValuePair<string, object?>("automation.alias", automationAlias));

    // === Steps ===

    public void StepExecuted(string actionAlias) =>
        _stepsExecuted.Add(1, new KeyValuePair<string, object?>("action.alias", actionAlias));

    public void StepFailed(string actionAlias) =>
        _stepsFailed.Add(1, new KeyValuePair<string, object?>("action.alias", actionAlias));

    public void RecordStepDuration(double durationMs, string actionAlias) =>
        _stepDuration.Record(durationMs, new KeyValuePair<string, object?>("action.alias", actionAlias));

    // === Triggers ===

    public void TriggerDispatched(string triggerAlias) =>
        _triggersDispatched.Add(1, new KeyValuePair<string, object?>("trigger.alias", triggerAlias));

    // === Rate Limiting ===

    public void RateLimitRejected(string automationAlias) =>
        _rateLimitRejections.Add(1, new KeyValuePair<string, object?>("automation.alias", automationAlias));

    // === Outbox ===

    public void OutboxMessagePublished(string topic) =>
        _outboxMessagesPublished.Add(1, new KeyValuePair<string, object?>("topic", topic));

    public void OutboxMessageCompleted(string topic) =>
        _outboxMessagesCompleted.Add(1, new KeyValuePair<string, object?>("topic", topic));

    public void OutboxMessageDeadLettered(string topic) =>
        _outboxMessagesDeadLettered.Add(1, new KeyValuePair<string, object?>("topic", topic));

    public void OutboxBackpressureRejection() =>
        _outboxBackpressureRejections.Add(1);

    /// <summary>
    /// Updates the cached outbox depth values. Called periodically by the <c>OutboxDispatcher</c>.
    /// </summary>
    public void UpdateOutboxDepth(int pending, int processing)
    {
        _outboxPending = pending;
        _outboxProcessing = processing;
    }

    private IEnumerable<Measurement<int>> ObserveOutboxDepth() =>
    [
        new Measurement<int>(_outboxPending, new KeyValuePair<string, object?>("status", "pending")),
        new Measurement<int>(_outboxProcessing, new KeyValuePair<string, object?>("status", "processing")),
    ];
}
