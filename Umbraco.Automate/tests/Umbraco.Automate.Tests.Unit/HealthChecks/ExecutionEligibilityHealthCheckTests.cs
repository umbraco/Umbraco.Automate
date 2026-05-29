using Shouldly;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.HealthChecks;
using Umbraco.Cms.Core.HealthChecks;

namespace Umbraco.Automate.Tests.Unit.HealthChecks;

public class ExecutionEligibilityHealthCheckTests
{
    [Fact]
    public async Task Returns_success_when_node_is_eligible()
    {
        var check = BuildCheck(eligible: true, reason: null, pending: 5);

        var status = (await check.GetStatusAsync()).Single();

        status.ResultType.ShouldBe(StatusResultType.Success);
    }

    [Fact]
    public async Task Returns_error_when_ineligible_and_work_is_queued()
    {
        var check = BuildCheck(
            eligible: false,
            reason: "ServerRole is Unknown — set DisableElectionForSingleServer to true.",
            pending: 9);

        var status = (await check.GetStatusAsync()).Single();

        status.ResultType.ShouldBe(StatusResultType.Error);
        status.Message.ShouldContain("9");
        status.Message.ShouldContain("DisableElectionForSingleServer");
    }

    [Fact]
    public async Task Returns_warning_when_ineligible_but_nothing_queued()
    {
        var check = BuildCheck(
            eligible: false,
            reason: "ServerRole is Subscriber — this is expected on subscriber nodes.",
            pending: 0);

        var status = (await check.GetStatusAsync()).Single();

        status.ResultType.ShouldBe(StatusResultType.Warning);
        status.Message.ShouldContain("Subscriber");
    }

    private static ExecutionEligibilityHealthCheck BuildCheck(bool eligible, string? reason, int pending)
    {
        var eligibility = new Mock<IExecutionNodeEligibility>();
        eligibility.Setup(e => e.CanExecuteWorkflows(out reason)).Returns(eligible);

        var healthService = new Mock<IAutomateHealthService>();
        healthService.Setup(s => s.GetQueueStatsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomateQueueStats { Pending = pending });

        return new ExecutionEligibilityHealthCheck(eligibility.Object, healthService.Object);
    }
}
