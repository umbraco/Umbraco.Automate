using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Tests.Common.Builders;

/// <summary>
/// Fluent builder for <see cref="AutomationRun"/> test instances.
/// </summary>
public class AutomationRunBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _automationId = Guid.NewGuid();
    private int _automationVersion = 1;
    private AutomationRunStatus _status = AutomationRunStatus.Running;
    private DateTime _startedUtc = DateTime.UtcNow;
    private DateTime? _completedUtc;
    private string _initiatedBy = "system";
    private string? _correlationId;
    private string? _triggerData;
    private string? _error;
    private IList<StepRun> _stepRuns = [];

    public AutomationRunBuilder WithId(Guid id) { _id = id; return this; }
    public AutomationRunBuilder WithAutomationId(Guid automationId) { _automationId = automationId; return this; }
    public AutomationRunBuilder WithAutomationVersion(int version) { _automationVersion = version; return this; }
    public AutomationRunBuilder WithStatus(AutomationRunStatus status) { _status = status; return this; }
    public AutomationRunBuilder WithStartedUtc(DateTime startedUtc) { _startedUtc = startedUtc; return this; }
    public AutomationRunBuilder WithCompletedUtc(DateTime? completedUtc) { _completedUtc = completedUtc; return this; }
    public AutomationRunBuilder WithInitiatedBy(string initiatedBy) { _initiatedBy = initiatedBy; return this; }
    public AutomationRunBuilder WithCorrelationId(string? correlationId) { _correlationId = correlationId; return this; }
    public AutomationRunBuilder WithTriggerData(string? triggerData) { _triggerData = triggerData; return this; }
    public AutomationRunBuilder WithError(string? error) { _error = error; return this; }

    public AutomationRunBuilder AsCompleted()
    {
        _status = AutomationRunStatus.Completed;
        _completedUtc = DateTime.UtcNow;
        return this;
    }

    public AutomationRunBuilder AsFailed(string error = "Test failure")
    {
        _status = AutomationRunStatus.Failed;
        _completedUtc = DateTime.UtcNow;
        _error = error;
        return this;
    }

    public AutomationRun Build() => new()
    {
        Id = _id,
        AutomationId = _automationId,
        AutomationVersion = _automationVersion,
        Status = _status,
        StartedUtc = _startedUtc,
        CompletedUtc = _completedUtc,
        InitiatedBy = _initiatedBy,
        CorrelationId = _correlationId,
        TriggerData = _triggerData,
        Error = _error,
        StepRuns = _stepRuns,
    };

    public static implicit operator AutomationRun(AutomationRunBuilder builder) => builder.Build();
}
