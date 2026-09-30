using Umbraco.Automate.Core.Execution;

namespace Umbraco.Automate.Tests.Unit.Execution;

public class WorkflowLockOptionsValidatorTests
{
    private readonly WorkflowLockOptionsValidator _validator = new();

    [Fact]
    public void Defaults_AreValid()
    {
        _validator.Validate(null, new WorkflowLockOptions()).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData(30, 30)]
    [InlineData(30, 45)]
    public void RenewalIntervalNotShorterThanLeaseDuration_Fails(int leaseSeconds, int renewalSeconds)
    {
        var result = _validator.Validate(null, new WorkflowLockOptions
        {
            LeaseDuration = TimeSpan.FromSeconds(leaseSeconds),
            RenewalInterval = TimeSpan.FromSeconds(renewalSeconds),
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("RenewalInterval");
    }

    [Fact]
    public void NonPositiveDurations_Fail()
    {
        var result = _validator.Validate(null, new WorkflowLockOptions
        {
            LeaseDuration = TimeSpan.Zero,
            RenewalInterval = TimeSpan.FromSeconds(-1),
        });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("LeaseDuration must be greater than zero");
        result.FailureMessage.ShouldContain("RenewalInterval must be greater than zero");
    }
}
