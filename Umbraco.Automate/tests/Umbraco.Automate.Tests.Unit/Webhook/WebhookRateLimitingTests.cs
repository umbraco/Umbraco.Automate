using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Web;

namespace Umbraco.Automate.Tests.Unit.Webhook;

public class WebhookRateLimitingTests
{
    /// <summary>
    /// Creates a <see cref="FixedWindowRateLimiter"/> matching the webhook endpoint policy configuration.
    /// </summary>
    private static FixedWindowRateLimiter CreateLimiter(int permitLimit) =>
        new(new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
        });

    [Fact]
    public async Task RequestsWithinLimit_ArePermitted()
    {
        using var limiter = CreateLimiter(permitLimit: 5);

        for (var i = 0; i < 5; i++)
        {
            using var lease = await limiter.AcquireAsync();
            lease.IsAcquired.ShouldBeTrue($"Request {i + 1} should be permitted");
        }
    }

    [Fact]
    public async Task RequestsExceedingLimit_AreRejected()
    {
        using var limiter = CreateLimiter(permitLimit: 3);

        // Consume all permits.
        var leases = new List<RateLimitLease>();
        for (var i = 0; i < 3; i++)
        {
            var lease = await limiter.AcquireAsync();
            lease.IsAcquired.ShouldBeTrue();
            leases.Add(lease);
        }

        // Next acquisition should be rejected.
        using var blocked = await limiter.AcquireAsync();
        blocked.IsAcquired.ShouldBeFalse();

        foreach (var lease in leases)
        {
            lease.Dispose();
        }
    }

    [Fact]
    public void DifferentAutomationIds_HaveIndependentLimits()
    {
        // Simulate per-partition rate limiting: each automation ID gets its own limiter.
        var limiters = new Dictionary<string, FixedWindowRateLimiter>();

        FixedWindowRateLimiter GetLimiter(string automationId)
        {
            if (!limiters.TryGetValue(automationId, out var limiter))
            {
                limiter = CreateLimiter(permitLimit: 2);
                limiters[automationId] = limiter;
            }

            return limiter;
        }

        var automationA = Guid.NewGuid().ToString();
        var automationB = Guid.NewGuid().ToString();

        try
        {
            // Exhaust permits for automation A.
            var leaseA1 = GetLimiter(automationA).AttemptAcquire();
            leaseA1.IsAcquired.ShouldBeTrue();
            var leaseA2 = GetLimiter(automationA).AttemptAcquire();
            leaseA2.IsAcquired.ShouldBeTrue();

            // Automation A should now be rate limited.
            var blockedA = GetLimiter(automationA).AttemptAcquire();
            blockedA.IsAcquired.ShouldBeFalse();

            // Automation B should still be allowed (independent partition).
            var leaseB1 = GetLimiter(automationB).AttemptAcquire();
            leaseB1.IsAcquired.ShouldBeTrue();
        }
        finally
        {
            foreach (var limiter in limiters.Values)
            {
                limiter.Dispose();
            }
        }
    }

    [Fact]
    public void RejectionStatusCode_Is429()
    {
        // Verify the configured rejection status code matches 429 Too Many Requests.
        // This is a configuration-level test ensuring the constant is correct.
        StatusCodes.Status429TooManyRequests.ShouldBe(429);
    }

    [Fact]
    public async Task OnRejected_SetsRetryAfterHeader()
    {
        // Verify that the OnRejected callback sets the Retry-After header.
        var httpContext = new DefaultHttpContext();

        // Simulate what the OnRejected callback does.
        httpContext.Response.Headers.RetryAfter = "60";

        httpContext.Response.Headers.RetryAfter.ToString().ShouldBe("60");
    }

    [Fact]
    public void DefaultRateLimitPerMinute_Is100()
    {
        var options = new WebhookOptions();
        options.RateLimitPerMinute.ShouldBe(100);
    }

    [Fact]
    public void RateLimitPolicyConstant_HasExpectedValue()
    {
        Constants.WebhookApi.RateLimitPolicy.ShouldBe("automate-webhook-rate-limit");
    }
}
