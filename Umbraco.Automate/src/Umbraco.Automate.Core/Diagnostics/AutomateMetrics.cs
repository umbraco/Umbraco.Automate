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
    private readonly Counter<long> _stepsExecuted;
    private readonly Counter<long> _stepsFailed;
    private readonly Counter<long> _triggersDispatched;
    private readonly Counter<long> _outboxMessagesPublished;
    private readonly Counter<long> _outboxMessagesCompleted;
    private readonly Counter<long> _outboxMessagesDeadLettered;
    private readonly Counter<long> _outboxBackpressureRejections;

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

        _stepsExecuted = meter.CreateCounter<long>(
            "automate.steps.executed",
            description: "Number of action steps executed");

        _stepsFailed = meter.CreateCounter<long>(
            "automate.steps.failed",
            description: "Number of action steps that failed");

        _triggersDispatched = meter.CreateCounter<long>(
            "automate.triggers.dispatched",
            description: "Number of trigger events dispatched to the outbox");

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
    }

    public void RunStarted(string automationAlias) =>
        _automationRunsStarted.Add(1, new KeyValuePair<string, object?>("automation.alias", automationAlias));

    public void RunCompleted(string automationAlias) =>
        _automationRunsCompleted.Add(1, new KeyValuePair<string, object?>("automation.alias", automationAlias));

    public void RunFailed(string automationAlias) =>
        _automationRunsFailed.Add(1, new KeyValuePair<string, object?>("automation.alias", automationAlias));

    public void StepExecuted(string actionAlias) =>
        _stepsExecuted.Add(1, new KeyValuePair<string, object?>("action.alias", actionAlias));

    public void StepFailed(string actionAlias) =>
        _stepsFailed.Add(1, new KeyValuePair<string, object?>("action.alias", actionAlias));

    public void TriggerDispatched(string triggerAlias) =>
        _triggersDispatched.Add(1, new KeyValuePair<string, object?>("trigger.alias", triggerAlias));

    public void OutboxMessagePublished(string topic) =>
        _outboxMessagesPublished.Add(1, new KeyValuePair<string, object?>("topic", topic));

    public void OutboxMessageCompleted(string topic) =>
        _outboxMessagesCompleted.Add(1, new KeyValuePair<string, object?>("topic", topic));

    public void OutboxMessageDeadLettered(string topic) =>
        _outboxMessagesDeadLettered.Add(1, new KeyValuePair<string, object?>("topic", topic));

    public void OutboxBackpressureRejection() =>
        _outboxBackpressureRejections.Add(1);
}
