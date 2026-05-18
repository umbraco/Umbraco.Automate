using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class EntityTypesFilterTests
{
    [Fact]
    public void NullFilter_MatchesAnyEntityType()
        => EntityTypesFilter.Matches(Guid.NewGuid(), null).ShouldBeTrue();

    [Fact]
    public void EmptyFilter_MatchesAnyEntityType()
        => EntityTypesFilter.Matches(Guid.NewGuid(), string.Empty).ShouldBeTrue();

    [Fact]
    public void WhitespaceFilter_MatchesAnyEntityType()
        => EntityTypesFilter.Matches(Guid.NewGuid(), "   ").ShouldBeTrue();

    [Fact]
    public void SingleAllowedType_MatchesExact()
    {
        var key = Guid.NewGuid();
        EntityTypesFilter.Matches(key, key.ToString()).ShouldBeTrue();
    }

    [Fact]
    public void SingleAllowedType_RejectsOther()
    {
        var allowed = Guid.NewGuid();
        var other = Guid.NewGuid();
        EntityTypesFilter.Matches(other, allowed.ToString()).ShouldBeFalse();
    }

    [Fact]
    public void CsvFilter_MatchesAnyOfTheListed()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        EntityTypesFilter.Matches(b, $"{a},{b},{c}").ShouldBeTrue();
    }

    [Fact]
    public void CsvFilter_TrimsWhitespace()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        EntityTypesFilter.Matches(b, $" {a} , {b} ").ShouldBeTrue();
    }

    [Fact]
    public void FilterSetButEntityTypeKeyNull_DoesNotMatch()
        => EntityTypesFilter.Matches(null, Guid.NewGuid().ToString()).ShouldBeFalse();

    [Fact]
    public void CsvFilter_IgnoresInvalidGuidSegments()
    {
        var allowed = Guid.NewGuid();
        EntityTypesFilter.Matches(allowed, $"not-a-guid,{allowed}").ShouldBeTrue();
    }
}
