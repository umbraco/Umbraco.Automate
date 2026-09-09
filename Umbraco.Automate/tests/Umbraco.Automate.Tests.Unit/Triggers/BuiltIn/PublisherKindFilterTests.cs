using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class PublisherKindFilterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Anyone")]
    [InlineData("anyone")]
    [InlineData("not-a-valid-value")]
    public void MissingOrAnyoneFilter_MatchesEveryKind(string? filter)
    {
        PublisherKindFilter.Matches(ContentPublisherKind.User, filter).ShouldBeTrue();
        PublisherKindFilter.Matches(ContentPublisherKind.Api, filter).ShouldBeTrue();
        PublisherKindFilter.Matches(ContentPublisherKind.System, filter).ShouldBeTrue();
        PublisherKindFilter.Matches(null, filter).ShouldBeTrue();
    }

    [Fact]
    public void UserFilter_MatchesOnlyBackofficeUsers()
    {
        PublisherKindFilter.Matches(ContentPublisherKind.User, nameof(PublishedByFilter.User)).ShouldBeTrue();
        PublisherKindFilter.Matches(ContentPublisherKind.Api, nameof(PublishedByFilter.User)).ShouldBeFalse();
        PublisherKindFilter.Matches(ContentPublisherKind.System, nameof(PublishedByFilter.User)).ShouldBeFalse();
    }

    [Fact]
    public void SystemFilter_MatchesSuperUserAndApiUsers()
    {
        PublisherKindFilter.Matches(ContentPublisherKind.System, nameof(PublishedByFilter.System)).ShouldBeTrue();
        PublisherKindFilter.Matches(ContentPublisherKind.Api, nameof(PublishedByFilter.System)).ShouldBeTrue();
        PublisherKindFilter.Matches(ContentPublisherKind.User, nameof(PublishedByFilter.System)).ShouldBeFalse();
    }

    [Theory]
    [InlineData("User")]
    [InlineData("System")]
    public void UnknownPublisher_DoesNotMatchSpecificFilters(string filter)
        => PublisherKindFilter.Matches(null, filter).ShouldBeFalse();

    [Fact]
    public void FilterValueIsCaseInsensitive()
    {
        PublisherKindFilter.Matches(ContentPublisherKind.User, "user").ShouldBeTrue();
        PublisherKindFilter.Matches(ContentPublisherKind.Api, "system").ShouldBeTrue();
    }
}
