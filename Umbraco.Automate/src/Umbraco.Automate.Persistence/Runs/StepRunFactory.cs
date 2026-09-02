using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Persistence.Runs;

/// <summary>
/// Maps between <see cref="StepRun"/> domain model and <see cref="StepRunEntity"/> EF entity.
/// </summary>
internal static class StepRunFactory
{
    public static StepRun BuildDomain(StepRunEntity entity)
    {
        return new StepRun
        {
            Id = entity.Id,
            RunId = entity.RunId,
            StepId = entity.StepId,
            ActionAlias = entity.ActionAlias,
            Status = (StepRunStatus)entity.Status,
            StartedUtc = entity.StartedUtc,
            CompletedUtc = entity.CompletedUtc,
            InputData = entity.InputData,
            OutputData = entity.OutputData,
            LogEntries = DeserializeLogEntries(entity.LogEntries),
            Error = entity.Error,
            ErrorCategory = entity.ErrorCategory.HasValue ? (StepRunErrorCategory)entity.ErrorCategory.Value : null,
            RetryCount = entity.RetryCount,
            Duration = entity.DurationTicks.HasValue ? TimeSpan.FromTicks(entity.DurationTicks.Value) : null,
        };
    }

    public static StepRunEntity BuildEntity(StepRun stepRun)
    {
        return new StepRunEntity
        {
            Id = stepRun.Id,
            RunId = stepRun.RunId,
            StepId = stepRun.StepId,
            ActionAlias = stepRun.ActionAlias,
            Status = (int)stepRun.Status,
            StartedUtc = stepRun.StartedUtc,
            CompletedUtc = stepRun.CompletedUtc,
            InputData = stepRun.InputData,
            OutputData = stepRun.OutputData,
            LogEntries = SerializeLogEntries(stepRun.LogEntries),
            Error = stepRun.Error,
            ErrorCategory = stepRun.ErrorCategory.HasValue ? (int)stepRun.ErrorCategory.Value : null,
            RetryCount = stepRun.RetryCount,
            DurationTicks = stepRun.Duration?.Ticks,
        };
    }

    public static void UpdateEntity(StepRunEntity entity, StepRun stepRun)
    {
        entity.Status = (int)stepRun.Status;
        entity.StartedUtc = stepRun.StartedUtc;
        entity.CompletedUtc = stepRun.CompletedUtc;
        entity.InputData = stepRun.InputData;
        entity.OutputData = stepRun.OutputData;
        entity.LogEntries = SerializeLogEntries(stepRun.LogEntries);
        entity.Error = stepRun.Error;
        entity.ErrorCategory = stepRun.ErrorCategory.HasValue ? (int)stepRun.ErrorCategory.Value : null;
        entity.RetryCount = stepRun.RetryCount;
        entity.DurationTicks = stepRun.Duration?.Ticks;
    }

    private static string? SerializeLogEntries(IReadOnlyList<ActionLogEntry> logEntries)
        => logEntries.Count > 0 ? JsonSerializer.Serialize(logEntries) : null;

    private static IReadOnlyList<ActionLogEntry> DeserializeLogEntries(string? logEntries)
        => logEntries is not null
            ? JsonSerializer.Deserialize<List<ActionLogEntry>>(logEntries) ?? []
            : [];
}
