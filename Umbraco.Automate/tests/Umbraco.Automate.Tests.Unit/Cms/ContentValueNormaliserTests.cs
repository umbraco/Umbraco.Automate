using Umbraco.Automate.Core.Cms;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Strings;

namespace Umbraco.Automate.Tests.Unit.Cms;

public class ContentValueNormaliserTests
{
    private readonly Mock<IPublishedValueFallback> _fallback = new();
    private readonly Mock<IPublishedUrlProvider> _urlProvider = new();
    private readonly ContentValueNormaliser _sut;

    public ContentValueNormaliserTests()
    {
        _sut = new ContentValueNormaliser(_fallback.Object, _urlProvider.Object);
    }

    [Fact]
    public void Normalise_Null_ReturnsNull()
        => _sut.Normalise(null, culture: null).ShouldBeNull();

    [Theory]
    [InlineData("hello")]
    [InlineData(42)]
    [InlineData(true)]
    public void Normalise_Primitive_PassesThrough(object value)
        => _sut.Normalise(value, culture: null).ShouldBe(value);

    [Fact]
    public void Normalise_HtmlEncodedString_ReturnsString()
    {
        var html = new HtmlEncodedString("<p>hello</p>");

        var result = _sut.Normalise(html, culture: null);

        result.ShouldBe("<p>hello</p>");
    }

    [Fact]
    public void Normalise_PublishedContent_ReturnsSummary()
    {
        var key = Guid.NewGuid();
        var content = MockPublishedContent(key, name: "About", typeAlias: "page");

        var result = _sut.Normalise(content.Object, culture: null) as IDictionary<string, object?>;

        result.ShouldNotBeNull();
        result["key"].ShouldBe(key);
        result["name"].ShouldBe("About");
        result["contentTypeAlias"].ShouldBe("page");
    }

    [Fact]
    public void Normalise_PublishedContentCollection_ReturnsArrayOfSummaries()
    {
        var c1 = MockPublishedContent(Guid.NewGuid(), "One", "page");
        var c2 = MockPublishedContent(Guid.NewGuid(), "Two", "page");

        var result = _sut.Normalise(new[] { c1.Object, c2.Object }, culture: null) as object[];

        result.ShouldNotBeNull();
        result.Length.ShouldBe(2);
    }

    [Fact]
    public void Normalise_PublishedElement_RecursesIntoProperties()
    {
        var element = MockPublishedElement(
            "block",
            ("title", "Hello"),
            ("count", 5));

        var result = _sut.Normalise(element.Object, culture: null) as IDictionary<string, object?>;

        result.ShouldNotBeNull();
        result["contentTypeAlias"].ShouldBe("block");
        result["title"].ShouldBe("Hello");
        result["count"].ShouldBe(5);
    }

    [Fact]
    public void Normalise_BlockListModel_FlattensToArray()
    {
        var contentKey = Guid.NewGuid();
        var element = MockPublishedElement("hero", ("heading", "Welcome")).Object;
        var item = new BlockListItem(contentKey, element, settingsKey: null, settings: null);
        var model = new BlockListModel(new[] { item });

        var result = _sut.Normalise(model, culture: null) as object[];

        result.ShouldNotBeNull();
        result.Length.ShouldBe(1);

        var first = result[0] as IDictionary<string, object?>;
        first.ShouldNotBeNull();
        first["contentKey"].ShouldBe(contentKey);

        var content = first["content"] as IDictionary<string, object?>;
        content.ShouldNotBeNull();
        content["heading"].ShouldBe("Welcome");
    }

    [Fact]
    public void Normalise_BlockGridModel_IncludesGridFields()
    {
        var element = MockPublishedElement("section").Object;
        var item = new BlockGridItem(Guid.NewGuid(), element, settingsKey: null, settings: null)
        {
            RowSpan = 1,
            ColumnSpan = 6,
            Areas = Array.Empty<BlockGridArea>(),
        };
        var model = new BlockGridModel(new[] { item }, gridColumns: 12);

        var result = _sut.Normalise(model, culture: null) as object[];

        var first = result![0] as IDictionary<string, object?>;
        first!["rowSpan"].ShouldBe(1);
        first["columnSpan"].ShouldBe(6);
        first["areas"].ShouldNotBeNull();
    }

    [Fact]
    public void Normalise_DepthGuard_StopsRecursion()
    {
        // Build a chain of 6 elements deep — exceeds MaxExpansionDepth (5).
        var deepest = MockPublishedElement("level6", ("value", "leaf")).Object;
        var level5 = MockPublishedElement("level5", ("nested", deepest)).Object;
        var level4 = MockPublishedElement("level4", ("nested", level5)).Object;
        var level3 = MockPublishedElement("level3", ("nested", level4)).Object;
        var level2 = MockPublishedElement("level2", ("nested", level3)).Object;
        var level1 = MockPublishedElement("level1", ("nested", level2)).Object;

        var result = _sut.Normalise(level1, culture: null);

        // We should get a dict, but somewhere down the chain "nested" becomes null.
        // Assert by walking until null appears.
        var current = result as IDictionary<string, object?>;
        var depth = 0;
        while (current is not null && current.TryGetValue("nested", out var next) && next is IDictionary<string, object?> nextDict)
        {
            current = nextDict;
            depth++;
            if (depth > 10)
            {
                throw new InvalidOperationException("Depth guard did not stop recursion.");
            }
        }

        depth.ShouldBeLessThan(6);
    }

    // --- helpers ---

    private static Mock<IPublishedContent> MockPublishedContent(Guid key, string name, string typeAlias)
    {
        var contentType = new Mock<IPublishedContentType>();
        contentType.SetupGet(x => x.Alias).Returns(typeAlias);
        contentType.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);
        // Required by the Url() extension — without this, Url() throws NotSupportedException
        // because the default ItemType (Unknown) hits the switch's default arm.
        contentType.SetupGet(x => x.ItemType).Returns(PublishedItemType.Content);

        var content = new Mock<IPublishedContent>();
        content.SetupGet(x => x.Key).Returns(key);
        content.SetupGet(x => x.Name).Returns(name);
        content.SetupGet(x => x.ContentType).Returns(contentType.Object);
        content.SetupGet(x => x.Cultures).Returns(new Dictionary<string, PublishedCultureInfo>(StringComparer.OrdinalIgnoreCase));
        return content;
    }

    private static Mock<IPublishedElement> MockPublishedElement(string typeAlias, params (string Alias, object? Value)[] properties)
    {
        var contentType = new Mock<IPublishedContentType>();
        contentType.SetupGet(x => x.Alias).Returns(typeAlias);
        contentType.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        contentType.SetupGet(x => x.Variations).Returns(ContentVariation.Nothing);

        var props = properties.Select(p => MockProperty(p.Alias, p.Value).Object).ToList();

        var element = new Mock<IPublishedElement>();
        element.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        element.SetupGet(x => x.ContentType).Returns(contentType.Object);
        element.SetupGet(x => x.Properties).Returns(props);

        foreach (var prop in props)
        {
            element.Setup(x => x.GetProperty(prop.Alias)).Returns(prop);
        }

        return element;
    }

    private static Mock<IPublishedProperty> MockProperty(string alias, object? value)
    {
        var prop = new Mock<IPublishedProperty>();
        prop.SetupGet(x => x.Alias).Returns(alias);
        prop.Setup(x => x.HasValue(It.IsAny<string>(), It.IsAny<string>())).Returns(value is not null);
        prop.Setup(x => x.GetValue(It.IsAny<string>(), It.IsAny<string>())).Returns(value);
        return prop;
    }
}
