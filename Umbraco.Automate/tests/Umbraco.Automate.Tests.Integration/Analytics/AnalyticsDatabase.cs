// Stories AN-2 to AN-6: docs/plans/analytics/STORIES.md
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Notifications.Channels;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Persistence.Analytics;
using Umbraco.Automate.Persistence.Automations;
using Umbraco.Automate.Persistence.Runs;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Tests.Common.Fixtures;

namespace Umbraco.Automate.Tests.Integration.Analytics;

/// <summary>
/// An in-memory SQLite database with the repositories the analytics specs seed and query, so each
/// scenario's constructor reads as its Given. Mirrors <see cref="RunMetricsRepositoryTests"/>'s setup.
/// </summary>
internal sealed class AnalyticsDatabase : IDisposable
{
    public static readonly DateTime WindowStartUtc = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    public static readonly DateTime WindowEndUtc = new(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly EfCoreTestFixture _fixture = new();
    private readonly EFCoreAutomationRepository _automations;
    private readonly EFCoreAutomationRunRepository _runs;

    public AnalyticsDatabase(int? maxSortedRows = null)
    {
        var factory = new TestDbContextFactory(_fixture.CreateContext);
        _automations = new EFCoreAutomationRepository(factory, CreateAutomationFactory());
        _runs = new EFCoreAutomationRunRepository(factory);
        Repository = maxSortedRows is { } limit
            ? new EFCoreAutomateAnalyticsRepository(factory, limit)
            : new EFCoreAutomateAnalyticsRepository(factory);
    }

    public EFCoreAutomateAnalyticsRepository Repository { get; }

    public Automation AddAutomation(Guid workspaceId, string name = "Sync new orders")
    {
        Automation automation = new AutomationBuilder().WithWorkspaceId(workspaceId).WithName(name).Build();
        _automations.SaveAsync(automation).GetAwaiter().GetResult();
        return automation;
    }

    public void MoveAutomation(Automation automation, Guid workspaceId)
    {
        automation.WorkspaceId = workspaceId;
        _automations.SaveAsync(automation).GetAwaiter().GetResult();
    }

    public void DeleteAutomation(Automation automation)
        => _automations.DeleteAsync(automation.Id).GetAwaiter().GetResult();

    public AutomationRun AddRun(
        Automation automation,
        AutomationRunStatus status,
        DateTime? startedUtc,
        TimeSpan? took = null)
    {
        var builder = new AutomationRunBuilder()
            .WithAutomationId(automation.Id)
            .WithWorkspaceId(automation.WorkspaceId)
            .WithStatus(status);

        if (startedUtc.HasValue)
        {
            builder = builder.WithStartedUtc(startedUtc.Value);
        }

        if (startedUtc.HasValue && took.HasValue)
        {
            builder = builder.WithCompletedUtc(startedUtc.Value + took.Value);
        }

        AutomationRun run = builder.Build();

        // The builder always sets a start time; a run that never started is built by clearing it.
        if (!startedUtc.HasValue)
        {
            run.StartedUtc = null;
        }

        _runs.SaveAsync(run).GetAwaiter().GetResult();
        return run;
    }

    public void AddStepRun(
        AutomationRun run,
        string actionAlias,
        StepRunStatus status,
        DateTime startedUtc,
        TimeSpan? took = null,
        int retryCount = 0,
        StepRunErrorCategory? errorCategory = null)
    {
        var stepRun = new StepRun
        {
            Id = Guid.NewGuid(),
            RunId = run.Id,
            StepId = Guid.NewGuid(),
            ActionAlias = actionAlias,
            Status = status,
            StartedUtc = startedUtc,
            CompletedUtc = took.HasValue ? startedUtc + took.Value : null,
            Duration = took,
            RetryCount = retryCount,
            ErrorCategory = errorCategory,
        };
        _runs.AddStepRunAsync(stepRun).GetAwaiter().GetResult();
    }

    public void Dispose() => _fixture.Dispose();

    private static AutomationFactory CreateAutomationFactory()
    {
        var serializer = new EditableModelSerializer(
            Mock.Of<ISensitiveFieldProtector>(p => p.IsProtected(It.IsAny<string>()) == false),
            new ConfigurationReferenceResolver(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));

        return new AutomationFactory(
            serializer,
            new AutomationSettingsProtector(
                serializer,
                new ActionCollection(Array.Empty<IAction>),
                new TriggerCollection(Array.Empty<ITrigger>),
                new WebhookAuthenticatorCollection(Array.Empty<IWebhookAuthenticator>),
                new NotificationChannelCollection(Array.Empty<INotificationChannel>)));
    }
}
