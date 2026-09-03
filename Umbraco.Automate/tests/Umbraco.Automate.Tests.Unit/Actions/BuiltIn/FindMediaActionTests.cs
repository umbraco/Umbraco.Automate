using Umbraco.Automate.Core.Actions.BuiltIn;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class FindMediaActionTests
{
    [Fact]
    public void BuildQuery_Exact_NoTypeFilter_ProducesPhraseQuery()
    {
        var query = FindMediaAction.BuildQuery(
            "Logo", FindContentMatchMode.Exact, mediaTypeAliases: []);

        query.ShouldBe("+__IndexType:media +(nodeName:\"logo\")");
    }

    [Fact]
    public void BuildQuery_StartsWith_UsesTrailingWildcard()
    {
        var query = FindMediaAction.BuildQuery(
            "logo", FindContentMatchMode.StartsWith, mediaTypeAliases: []);

        query.ShouldBe("+__IndexType:media +(nodeName:logo*)");
    }

    [Fact]
    public void BuildQuery_Contains_UsesLeadingAndTrailingWildcards()
    {
        var query = FindMediaAction.BuildQuery(
            "banner", FindContentMatchMode.Contains, mediaTypeAliases: []);

        query.ShouldBe("+__IndexType:media +(nodeName:*banner*)");
    }

    [Fact]
    public void BuildQuery_WithSingleMediaTypeAlias_AppendsAliasFilter()
    {
        var query = FindMediaAction.BuildQuery(
            "Logo", FindContentMatchMode.Exact, mediaTypeAliases: ["image"]);

        query.ShouldContain("+(__NodeTypeAlias:image)");
    }

    [Fact]
    public void BuildQuery_WithMultipleAliases_BracketedOr()
    {
        var query = FindMediaAction.BuildQuery(
            "Logo",
            FindContentMatchMode.Exact,
            mediaTypeAliases: ["image", "file", "vectorGraphics"]);

        query.ShouldContain("+(__NodeTypeAlias:image __NodeTypeAlias:file __NodeTypeAlias:vectorgraphics)");
    }

    [Fact]
    public void BuildQuery_EmptyAliasList_OmitsAliasFilter()
    {
        var query = FindMediaAction.BuildQuery(
            "Logo", FindContentMatchMode.Exact, mediaTypeAliases: []);

        query.ShouldNotContain("__NodeTypeAlias");
    }

    [Fact]
    public void BuildQuery_EscapesLuceneSpecialCharactersInName()
    {
        var query = FindMediaAction.BuildQuery(
            "product+new", FindContentMatchMode.Exact, mediaTypeAliases: []);

        query.ShouldContain(@"product\+new");
    }
}
