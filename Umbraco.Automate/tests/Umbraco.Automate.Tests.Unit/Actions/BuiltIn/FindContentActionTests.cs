using Umbraco.Automate.Core.Actions.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class FindContentActionTests
{
    [Fact]
    public void BuildQuery_Exact_NoCulture_NoTypeFilter_ProducesPhraseQuery()
    {
        var query = FindContentAction.BuildQuery(
            "Homepage", FindContentMatchMode.Exact, culture: null, contentTypeAliases: []);

        query.ShouldBe("+__IndexType:content +(nodeName:\"homepage\")");
    }

    [Fact]
    public void BuildQuery_StartsWith_UsesTrailingWildcard()
    {
        var query = FindContentAction.BuildQuery(
            "home", FindContentMatchMode.StartsWith, culture: null, contentTypeAliases: []);

        query.ShouldBe("+__IndexType:content +(nodeName:home*)");
    }

    [Fact]
    public void BuildQuery_Contains_UsesLeadingAndTrailingWildcards()
    {
        var query = FindContentAction.BuildQuery(
            "press", FindContentMatchMode.Contains, culture: null, contentTypeAliases: []);

        query.ShouldBe("+__IndexType:content +(nodeName:*press*)");
    }

    [Fact]
    public void BuildQuery_WithCulture_MatchesCultureFieldAndFallsBackToInvariant()
    {
        // Culture-specific content is indexed under nodeName_{culture}, but invariant
        // items only exist under nodeName — fall back to both so invariant content
        // surfaces when a culture is requested.
        var query = FindContentAction.BuildQuery(
            "Homepage", FindContentMatchMode.Exact, culture: "en-US", contentTypeAliases: []);

        query.ShouldBe("+__IndexType:content +(nodeName_en-us:\"homepage\" nodeName:\"homepage\")");
    }

    [Fact]
    public void BuildQuery_WithSingleContentTypeAlias_AppendsAliasFilter()
    {
        var query = FindContentAction.BuildQuery(
            "Homepage", FindContentMatchMode.Exact, culture: null, contentTypeAliases: ["blogPost"]);

        query.ShouldContain("+(__NodeTypeAlias:blogpost)");
    }

    [Fact]
    public void BuildQuery_WithMultipleAliases_BracketedOr()
    {
        // Multiple content type aliases are OR-ed within a required group so the whole
        // query fails if none match — not an AND across aliases, which would be impossible.
        var query = FindContentAction.BuildQuery(
            "Homepage",
            FindContentMatchMode.Exact,
            culture: null,
            contentTypeAliases: ["blogPost", "newsArticle", "pressRelease"]);

        query.ShouldContain("+(__NodeTypeAlias:blogpost __NodeTypeAlias:newsarticle __NodeTypeAlias:pressrelease)");
    }

    [Fact]
    public void BuildQuery_EmptyAliasList_OmitsAliasFilter()
    {
        var query = FindContentAction.BuildQuery(
            "Homepage", FindContentMatchMode.Exact, culture: null, contentTypeAliases: []);

        query.ShouldNotContain("__NodeTypeAlias");
    }

    [Fact]
    public void BuildQuery_EscapesLuceneSpecialCharactersInName()
    {
        // User-provided names may contain Lucene syntax (+, -, :, /, etc) — without
        // escaping, a name like "product+new" would be parsed as a Lucene operator and
        // either fail or match the wrong content. QueryParserBase.Escape handles this.
        var query = FindContentAction.BuildQuery(
            "Product+New", FindContentMatchMode.Exact, culture: null, contentTypeAliases: []);

        query.ShouldContain(@"product\+new");
    }
}
