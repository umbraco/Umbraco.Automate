using Shouldly;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Persistence.Runs;

namespace Umbraco.Automate.Tests.Unit.Persistence;

public class AutomationRunFactoryTests
{
    [Fact]
    public void AutomationRun_BuildEntity_AndBuildDomain_RoundTrips()
    {
        var run = new AutomationRun
        {
            Id = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            AutomationVersion = 3,
            WorkspaceId = Guid.NewGuid(),
            ServiceAccountKey = Guid.NewGuid(),
            Status = AutomationRunStatus.Running,
            StartedUtc = DateTime.UtcNow,
            TriggerData = "{\"key\":\"value\"}",
            InitiatedBy = "user",
            CorrelationId = "corr-123",
        };

        AutomationRunEntity entity = AutomationRunFactory.BuildEntity(run);
        AutomationRun roundTripped = AutomationRunFactory.BuildDomain(entity);

        roundTripped.Id.ShouldBe(run.Id);
        roundTripped.AutomationId.ShouldBe(run.AutomationId);
        roundTripped.AutomationVersion.ShouldBe(3);
        roundTripped.WorkspaceId.ShouldBe(run.WorkspaceId);
        roundTripped.ServiceAccountKey.ShouldBe(run.ServiceAccountKey);
        roundTripped.Status.ShouldBe(AutomationRunStatus.Running);
        roundTripped.InitiatedBy.ShouldBe("user");
        roundTripped.CorrelationId.ShouldBe("corr-123");
    }

    [Fact]
    public void StepRun_BuildEntity_AndBuildDomain_RoundTrips()
    {
        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "test.action",
            Status = StepRunStatus.Failed,
            Error = "Something went wrong",
            ErrorCategory = StepRunErrorCategory.Timeout,
            RetryCount = 2,
            Duration = TimeSpan.FromMilliseconds(1234),
            LogEntries =
            [
                new ActionLogEntry(DateTime.UtcNow, ActionLogLevel.Info, "Starting"),
                new ActionLogEntry(DateTime.UtcNow, ActionLogLevel.Error, "Boom"),
            ],
        };

        StepRunEntity entity = StepRunFactory.BuildEntity(stepRun);
        StepRun roundTripped = StepRunFactory.BuildDomain(entity);

        roundTripped.Id.ShouldBe(stepRun.Id);
        roundTripped.ActionAlias.ShouldBe("test.action");
        roundTripped.Status.ShouldBe(StepRunStatus.Failed);
        roundTripped.ErrorCategory.ShouldBe(StepRunErrorCategory.Timeout);
        roundTripped.RetryCount.ShouldBe(2);
        roundTripped.Duration.ShouldBe(TimeSpan.FromMilliseconds(1234));
        roundTripped.LogEntries.Count.ShouldBe(2);
        roundTripped.LogEntries[0].Level.ShouldBe(ActionLogLevel.Info);
        roundTripped.LogEntries[0].Message.ShouldBe("Starting");
        roundTripped.LogEntries[1].Level.ShouldBe(ActionLogLevel.Error);
        roundTripped.LogEntries[1].Message.ShouldBe("Boom");
    }

    [Fact]
    public void StepRun_NullOptionals_RoundTripsAsNull()
    {
        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = Guid.NewGuid(),
            StepId = Guid.NewGuid(),
            ActionAlias = "test.action",
            ErrorCategory = null,
            Duration = null,
        };

        StepRunEntity entity = StepRunFactory.BuildEntity(stepRun);
        StepRun roundTripped = StepRunFactory.BuildDomain(entity);

        roundTripped.ErrorCategory.ShouldBeNull();
        roundTripped.Duration.ShouldBeNull();
        entity.LogEntries.ShouldBeNull();
        roundTripped.LogEntries.ShouldBeEmpty();
    }
}
