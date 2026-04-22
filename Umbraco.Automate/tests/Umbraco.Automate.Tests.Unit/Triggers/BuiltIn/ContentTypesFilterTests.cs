using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public class ContentTypesFilterTests
{
    [Fact]
    public void NullFilter_MatchesAnyContentType()
        => ContentTypesFilter.Matches(Guid.NewGuid(), null).ShouldBeTrue();

    [Fact]
    public void EmptyFilter_MatchesAnyContentType()
        => ContentTypesFilter.Matches(Guid.NewGuid(), string.Empty).ShouldBeTrue();

    [Fact]
    public void WhitespaceFilter_MatchesAnyContentType()
        => ContentTypesFilter.Matches(Guid.NewGuid(), "   ").ShouldBeTrue();

    [Fact]
    public void SingleAllowedType_MatchesExact()
    {
        var key = Guid.NewGuid();
        ContentTypesFilter.Matches(key, key.ToString()).ShouldBeTrue();
    }

    [Fact]
    public void SingleAllowedType_RejectsOther()
    {
        var allowed = Guid.NewGuid();
        var other = Guid.NewGuid();
        ContentTypesFilter.Matches(other, allowed.ToString()).ShouldBeFalse();
    }

    [Fact]
    public void CsvFilter_MatchesAnyOfTheListed()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var c = Guid.NewGuid();
        ContentTypesFilter.Matches(b, $"{a},{b},{c}").ShouldBeTrue();
    }

    [Fact]
    public void CsvFilter_TrimsWhitespace()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        ContentTypesFilter.Matches(b, $" {a} , {b} ").ShouldBeTrue();
    }

    [Fact]
    public void FilterSetButContentTypeKeyNull_DoesNotMatch()
        => ContentTypesFilter.Matches(null, Guid.NewGuid().ToString()).ShouldBeFalse();

    [Fact]
    public void CsvFilter_IgnoresInvalidGuidSegments()
    {
        var allowed = Guid.NewGuid();
        ContentTypesFilter.Matches(allowed, $"not-a-guid,{allowed}").ShouldBeTrue();
    }
}
