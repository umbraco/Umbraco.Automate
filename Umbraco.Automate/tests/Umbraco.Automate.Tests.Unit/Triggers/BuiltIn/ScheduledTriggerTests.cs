using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ScheduledTriggerTests
{
    private readonly ScheduledTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoAutomate.scheduled");

    [Fact]
    public void GetCronExpression_ReturnsConfiguredValue()
    {
        var settings = new ScheduledTriggerSettings { CronExpression = "0 9 * * 1-5" };

        _trigger.GetCronExpression(settings).ShouldBe("0 9 * * 1-5");
    }

    [Fact]
    public void GetCronExpression_FallsBackToHourly_WhenSettingsMissing()
        => _trigger.GetCronExpression(null).ShouldBe("0 * * * *");

    [Fact]
    public void GetCronExpression_FallsBackToHourly_WhenExpressionBlank()
    {
        var settings = new ScheduledTriggerSettings { CronExpression = "   " };

        _trigger.GetCronExpression(settings).ShouldBe("0 * * * *");
    }

    [Fact]
    public void GetTimeZone_ReturnsUtc_WhenSettingsMissing()
        => _trigger.GetTimeZone(null).Id.ShouldBe(TimeZoneInfo.Utc.Id);

    [Fact]
    public void GetTimeZone_ReturnsUtc_WhenTimeZoneEmpty()
    {
        var settings = new ScheduledTriggerSettings { TimeZone = null };

        _trigger.GetTimeZone(settings).Id.ShouldBe(TimeZoneInfo.Utc.Id);
    }

    [Fact]
    public void GetTimeZone_ReturnsUtc_WhenTimeZoneWhitespace()
    {
        var settings = new ScheduledTriggerSettings { TimeZone = "   " };

        _trigger.GetTimeZone(settings).Id.ShouldBe(TimeZoneInfo.Utc.Id);
    }

    [Theory]
    [InlineData("Europe/Copenhagen")]
    [InlineData("Europe/London")]
    [InlineData("America/New_York")]
    [InlineData("UTC")]
    public void GetTimeZone_ResolvesValidIanaId(string ianaId)
    {
        var settings = new ScheduledTriggerSettings { TimeZone = ianaId };

        var resolved = _trigger.GetTimeZone(settings);

        // .NET normalizes id casing; compare against a system lookup to remain platform-agnostic.
        resolved.Id.ShouldBe(TimeZoneInfo.FindSystemTimeZoneById(ianaId).Id);
    }

    [Fact]
    public void GetTimeZone_FallsBackToUtc_WhenIdInvalid()
    {
        var settings = new ScheduledTriggerSettings { TimeZone = "Not/A_Real_Zone" };

        _trigger.GetTimeZone(settings).Id.ShouldBe(TimeZoneInfo.Utc.Id);
    }
}
