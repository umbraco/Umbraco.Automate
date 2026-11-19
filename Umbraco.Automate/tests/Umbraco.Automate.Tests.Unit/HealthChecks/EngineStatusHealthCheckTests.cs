using Umbraco.Automate.Core;
using Umbraco.Automate.Core.HealthChecks;
using Umbraco.Cms.Core.HealthChecks;

namespace Umbraco.Automate.Tests.Unit.HealthChecks;

public class EngineStatusHealthCheckTests
{
    private readonly AutomateReadinessSignal _readinessSignal = new();
    private readonly Mock<IAutomateHealthService> _healthService = new();

    public EngineStatusHealthCheckTests()
    {
        _readinessSignal.Signal();
        _healthService
            .Setup(s => s.GetQueueStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomateQueueStats());
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsSuccess_WhenEngineIsRunning()
    {
        var status = await GetSingleStatusAsync();

        status.ResultType.ShouldBe(StatusResultType.Success);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsError_WhenEngineStartFailed()
    {
        _readinessSignal.SignalEngineStartFailed();

        var status = await GetSingleStatusAsync();

        status.ResultType.ShouldBe(StatusResultType.Error);
    }

    [Fact]
    public async Task GetStatusAsync_ExplainsRecoveryFailure_WhenEngineStartFailed()
    {
        _readinessSignal.SignalEngineStartFailed();

        var status = await GetSingleStatusAsync();

        status.Message.ShouldContain("startup recovery failed");
    }

    private async Task<HealthCheckStatus> GetSingleStatusAsync()
    {
        var check = new EngineStatusHealthCheck(_readinessSignal, _healthService.Object);
        return (await check.GetStatusAsync()).Single();
    }
}
