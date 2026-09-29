using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Automate.Tests.Unit.Runs;

/// <summary>
/// Covers the default interface implementations of <see cref="IAutomationRunService.GetStepRunDataAsync"/>
/// and <see cref="IAutomationRunService.GetTriggerDataAsync"/>, which keep implementations written
/// before those members existed compiling and working. The defaults resolve their dependencies
/// through the process-wide <see cref="StaticServiceProvider"/>, so these tests swap it and run in
/// a non-parallel collection.
/// </summary>
[Collection(StaticServiceProviderCollection.Name)]
public sealed class AutomationRunServiceDefaultImplementationTests : IDisposable
{
    private readonly Mock<IAutomationRunRepository> _runRepo = new();
    private readonly IServiceProvider? _originalServiceProvider;
    private readonly IAutomationRunService _service = new LegacyAutomationRunService();

    public AutomationRunServiceDefaultImplementationTests()
    {
        _originalServiceProvider = StaticServiceProvider.Instance;

        var services = new ServiceCollection();
        services.AddSingleton(_runRepo.Object);
        services.AddSingleton<IRunDataSanitizer>(new RunDataSanitizer(
            new ActionCollection(() => []),
            new ControlFlowCollection(() => []),
            NullLogger<RunDataSanitizer>.Instance));
        StaticServiceProvider.Instance = services.BuildServiceProvider();
    }

    public void Dispose() => StaticServiceProvider.Instance = _originalServiceProvider!;

    [Fact]
    public async Task GetStepRunData_Default_ReturnsSanitizedDataFromTheRepository()
    {
        var stored = new StoredStepRunData
        {
            RunId = Guid.NewGuid(),
            StepRunId = Guid.NewGuid(),
            AutomationId = Guid.NewGuid(),
            ActionAlias = "test.action",
            InputData = """{"url":"https://example.com"}""",
            OutputData = """{"token":"xyz"}""",
        };
        _runRepo
            .Setup(r => r.GetStepRunDataAsync(stored.RunId, stored.StepRunId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var data = await _service.GetStepRunDataAsync(stored.RunId, stored.StepRunId);

        data.ShouldNotBeNull();
        data.AutomationId.ShouldBe(stored.AutomationId);
        data.ActionAlias.ShouldBe(stored.ActionAlias);
        JsonNode.Parse(data.Input!)!["url"]!.GetValue<string>().ShouldBe("https://example.com");
        JsonNode.Parse(data.Output!)!["token"]!.GetValue<string>().ShouldBe(SensitiveDataMasker.MaskedValue);
        data.Output!.ShouldNotContain("xyz");
    }

    [Fact]
    public async Task GetStepRunData_Default_UnknownStepRun_ReturnsNull()
    {
        var data = await _service.GetStepRunDataAsync(Guid.NewGuid(), Guid.NewGuid());

        data.ShouldBeNull();
    }

    [Fact]
    public async Task GetTriggerData_Default_ReturnsSanitizedDataFromTheRepository()
    {
        var runId = Guid.NewGuid();
        _runRepo
            .Setup(r => r.GetTriggerDataAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredRunTriggerData
            {
                RunId = runId,
                AutomationId = Guid.NewGuid(),
                TriggerData = """{"apiKey":"k","contentId":42}""",
            });

        var data = await _service.GetTriggerDataAsync(runId);

        data.ShouldNotBeNull();
        data.RunId.ShouldBe(runId);
        var node = JsonNode.Parse(data.TriggerData!)!;
        node["apiKey"]!.GetValue<string>().ShouldBe(SensitiveDataMasker.MaskedValue);
        node["contentId"]!.GetValue<int>().ShouldBe(42);
    }

    [Fact]
    public async Task GetTriggerData_Default_UnknownRun_ReturnsNull()
    {
        var data = await _service.GetTriggerDataAsync(Guid.NewGuid());

        data.ShouldBeNull();
    }

    /// <summary>
    /// An implementation written against the interface before the run data members were added.
    /// Only the two members under test matter; the rest are never called.
    /// </summary>
    private sealed class LegacyAutomationRunService : IAutomationRunService
    {
        public Task<AutomationRun?> GetRunAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(IEnumerable<AutomationRun> Items, int Total)> GetRunsByAutomationPagedAsync(
            Guid automationId, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<(IReadOnlyList<AutomationRunListItem> Items, int Total)> GetRunsPagedAsync(
            IReadOnlySet<Guid>? workspaceIds, int skip = 0, int take = 100, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<AutomationRunStatus?> GetPreviousTerminalRunStatusAsync(
            Guid automationId, Guid currentRunId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<AutomationRunStatus>> GetRecentTerminalStatusesAsync(
            Guid automationId, int windowSize, DateTime since, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<(AutomationRun Run, StepRun StepRun)>> GetStepRunsByStatusAsync(
            string actionAlias, StepRunStatus status, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RunSummary> GetRunSummaryAsync(
            IReadOnlySet<Guid>? workspaceIds = null, DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<AutomationRunCount>> GetRunCountsByAutomationAsync(
            IReadOnlySet<Guid>? workspaceIds = null, DateTime? from = null, DateTime? to = null, int take = 10, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RunLifecycleResult> SuspendRunAsync(Guid runId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RunLifecycleResult> ResumeRunAsync(Guid runId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<RunLifecycleResult> TerminateRunAsync(Guid runId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }
}

/// <summary>
/// Serialises tests that replace the process-wide <see cref="StaticServiceProvider"/>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class StaticServiceProviderCollection
{
    public const string Name = "StaticServiceProvider";
}
