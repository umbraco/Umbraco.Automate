using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Cms.Core.Extensions;
using Umbraco.Cms.Core.Search.Querying.Filtering;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class FindByNameSearchTests
{
    private static readonly Guid RecycleBin = UmbracoConstants.System.RecycleBinContentKey;

    [Theory]
    [InlineData("Press Release", new[] { "press", "release" })]
    [InlineData("  Press   Release  ", new[] { "press", "release" })]
    [InlineData("Children's Books", new[] { "children's", "books" })]
    [InlineData("O'Brien", new[] { "o'brien" })]
    [InlineData("Release 2.0", new[] { "release", "2.0" })]
    [InlineData("Product+New", new[] { "product+new" })]
    [InlineData("*press ?rel", new[] { "press", "rel" })]
    [InlineData("Édition", new[] { "édition" })]
    public void GetTerms_SplitsOnWhitespaceOnlyAndLowerCases(string name, string[] expected)
        => FindByNameSearch.GetTerms(name).ShouldBe(expected);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("* ??")]
    public void GetTerms_NothingToSearchFor_ReturnsNone(string name)
        => FindByNameSearch.GetTerms(name).ShouldBeEmpty();

    [Fact]
    public void BuildFilters_AddsOneTextFilterPerTermOnTheNameField()
    {
        var filters = FindByNameSearch.BuildFilters("Children's Books", [], RecycleBin);

        var textFilters = filters.OfType<TextFilter>().ToList();
        textFilters.ShouldAllBe(f => f.FieldName == UmbracoConstants.Search.FieldNames.Name && !f.Negate);
        textFilters.Select(f => f.Values.Single()).ShouldBe(["children's", "books"]);
    }

    [Theory]
    [InlineData("the homepage", new[] { "homepage" })]
    [InlineData("Terms and Conditions", new[] { "terms", "conditions" })]
    [InlineData("News & Events", new[] { "news", "events" })]
    [InlineData("hello-xyz", new[] { "hello-xyz" })]
    public void BuildFilters_LeavesOutTermsTheIndexCannotHold(string name, string[] expectedTerms)
        => FindByNameSearch.BuildFilters(name, [], RecycleBin)
            .OfType<TextFilter>()
            .Select(f => f.Values.Single())
            .ShouldBe(expectedTerms);

    [Fact]
    public void BuildFilters_NameOfOnlyUnindexableTerms_AddsNoTextFilter()
    {
        var filters = FindByNameSearch.BuildFilters("The And &", [], RecycleBin);

        filters.OfType<TextFilter>().ShouldBeEmpty();
        filters.ShouldContain(f => f.FieldName == UmbracoConstants.Search.FieldNames.PathIds);
    }

    [Theory]
    [InlineData("homepage", true)]
    [InlineData("c++", true)]
    [InlineData("o'brien", true)]
    [InlineData("the", false)]
    [InlineData("and", false)]
    [InlineData("&", false)]
    [InlineData("and/or", false)]
    [InlineData("q&a", true)]
    public void IsIndexable_NeedsALetterOrDigitOutsideTheStopWords(string term, bool expected)
        => FindByNameSearch.IsIndexable(term).ShouldBe(expected);

    [Fact]
    public void BuildFilters_NoTypeKeys_OmitsTheTypeFilter()
        => FindByNameSearch.BuildFilters("Homepage", [], RecycleBin)
            .OfType<KeywordFilter>()
            .ShouldNotContain(f => f.FieldName == UmbracoConstants.Search.FieldNames.ContentTypeId);

    [Fact]
    public void BuildFilters_WithTypeKeys_AddsOneKeywordFilterOrdingThem()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var filters = FindByNameSearch.BuildFilters("Homepage", [a, b], RecycleBin);

        var typeFilter = filters.OfType<KeywordFilter>().Single(f => f.FieldName == UmbracoConstants.Search.FieldNames.ContentTypeId);
        typeFilter.Negate.ShouldBeFalse();
        typeFilter.Values.ShouldBe([a.AsKeyword(), b.AsKeyword()]);
    }

    [Fact]
    public void BuildFilters_ExcludesTheRecycleBin()
    {
        var filters = FindByNameSearch.BuildFilters("Homepage", [], RecycleBin);

        var trashFilter = filters.OfType<KeywordFilter>().Single(f => f.FieldName == UmbracoConstants.Search.FieldNames.PathIds);
        trashFilter.Negate.ShouldBeTrue();
        trashFilter.Values.ShouldBe([RecycleBin.AsKeyword()]);
    }

    [Fact]
    public void ParseKeys_SkipsNonKeysAndBlankParts()
    {
        var key = Guid.NewGuid();

        FindByNameSearch.ParseKeys($" {key} , not-a-key,,").ShouldBe([key]);
        FindByNameSearch.ParseKeys(null).ShouldBeEmpty();
        FindByNameSearch.ParseKeys("  ").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Homepage", "homepage", FindContentMatchMode.Exact, true)]
    [InlineData("Homepage 2", "homepage", FindContentMatchMode.Exact, false)]
    [InlineData("  Press   Release ", "press release", FindContentMatchMode.Exact, true)]
    [InlineData("Press Release", "press  release", FindContentMatchMode.Exact, true)]
    [InlineData("The Homepage", "home", FindContentMatchMode.StartsWith, true)]
    [InlineData("Landing", "home", FindContentMatchMode.StartsWith, false)]
    [InlineData("Release Press", "press rel", FindContentMatchMode.StartsWith, true)]
    [InlineData("Press Archive", "press rel", FindContentMatchMode.StartsWith, false)]
    [InlineData("Hello-World Page", "hello-xyz", FindContentMatchMode.StartsWith, false)]
    [InlineData("Hello-World Page", "hello-world", FindContentMatchMode.StartsWith, true)]
    [InlineData("Plan C", "c++", FindContentMatchMode.StartsWith, false)]
    [InlineData("C++ Guide", "c++", FindContentMatchMode.StartsWith, true)]
    [InlineData("Q Branch", "q&a", FindContentMatchMode.StartsWith, false)]
    [InlineData("Terms and Conditions", "and cond", FindContentMatchMode.StartsWith, true)]
    [InlineData("Hallo-World Page", "world", FindContentMatchMode.StartsWith, true)]
    [InlineData("Q&A (FAQ)", "faq", FindContentMatchMode.StartsWith, true)]
    [InlineData("News&Events", "events", FindContentMatchMode.StartsWith, true)]
    [InlineData("[Draft] Landing", "draft", FindContentMatchMode.StartsWith, true)]
    [InlineData("Hallo-World Page", "orld", FindContentMatchMode.StartsWith, false)]
    [InlineData("Plan C", "c#", FindContentMatchMode.StartsWith, false)]
    [InlineData("*New* Products", "*New* Products", FindContentMatchMode.Exact, true)]
    [InlineData("*New* Products", "*new* prod", FindContentMatchMode.Contains, true)]
    [InlineData("New Products", "*New* Products", FindContentMatchMode.Exact, false)]
    [InlineData("The Press Release", "press rel", FindContentMatchMode.Contains, true)]
    [InlineData("The Press   Release", "press  release", FindContentMatchMode.Contains, true)]
    [InlineData("Release Press", "press rel", FindContentMatchMode.Contains, false)]
    [InlineData("Landing", "home", FindContentMatchMode.Contains, false)]
    [InlineData(null, "home", FindContentMatchMode.StartsWith, false)]
    public void IsNameMatch_AppliesModeIgnoringCase(string? itemName, string name, FindContentMatchMode mode, bool expected)
        => FindByNameSearch.IsNameMatch(itemName, name, mode).ShouldBe(expected);
}
