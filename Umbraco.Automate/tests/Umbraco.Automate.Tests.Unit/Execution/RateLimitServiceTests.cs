using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class RateLimitServiceTests
{
    private readonly Mock<IAutomationRunRepository> _runRepository = new();

    private RateLimitService CreateService(RateLimitingOptions? options = null)
    {
        options ??= new RateLimitingOptions();
        return new RateLimitService(
            Options.Create(options),
            _runRepository.Object);
    }

    [Fact]
    public async Task CheckRateLimitAsync_UnderLimit_DoesNotThrow()
    {
        var automationId = Guid.NewGuid();

        _runRepository
            .Setup(x => x.GetRecentRunCountAsync(automationId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        _runRepository
            .Setup(x => x.GetConcurrentRunCountAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var service = CreateService(new RateLimitingOptions
        {
            Enabled = true,
            MaxRunsPerAutomationPerMinute = 60,
            MaxConcurrentRunsPerAutomation = 10,
        });

        await Should.NotThrowAsync(() => service.CheckRateLimitAsync(automationId));
    }

    [Fact]
    public async Task CheckRateLimitAsync_ExceedsPerMinuteLimit_Throws()
    {
        var automationId = Guid.NewGuid();

        _runRepository
            .Setup(x => x.GetRecentRunCountAsync(automationId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(60);

        var service = CreateService(new RateLimitingOptions
        {
            Enabled = true,
            MaxRunsPerAutomationPerMinute = 60,
        });

        var ex = await Should.ThrowAsync<RateLimitExceededException>(
            () => service.CheckRateLimitAsync(automationId));

        ex.AutomationId.ShouldBe(automationId);
    }

    [Fact]
    public async Task CheckRateLimitAsync_ExceedsConcurrentLimit_Throws()
    {
        var automationId = Guid.NewGuid();

        _runRepository
            .Setup(x => x.GetRecentRunCountAsync(automationId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _runRepository
            .Setup(x => x.GetConcurrentRunCountAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(10);

        var service = CreateService(new RateLimitingOptions
        {
            Enabled = true,
            MaxRunsPerAutomationPerMinute = 60,
            MaxConcurrentRunsPerAutomation = 10,
        });

        var ex = await Should.ThrowAsync<RateLimitExceededException>(
            () => service.CheckRateLimitAsync(automationId));

        ex.AutomationId.ShouldBe(automationId);
    }

    [Fact]
    public async Task CheckRateLimitAsync_Disabled_AlwaysPasses()
    {
        var automationId = Guid.NewGuid();

        // Don't set up any repository calls — if rate limiting is disabled, they should not be called.
        var service = CreateService(new RateLimitingOptions { Enabled = false });

        await Should.NotThrowAsync(() => service.CheckRateLimitAsync(automationId));

        _runRepository.Verify(
            x => x.GetRecentRunCountAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
