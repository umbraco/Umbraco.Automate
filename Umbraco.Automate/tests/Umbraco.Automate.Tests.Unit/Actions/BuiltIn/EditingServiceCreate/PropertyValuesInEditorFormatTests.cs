// Story S4: docs/plans/537-create-via-editing-services/STORIES.md (#537)
using System.Text.Json.Nodes;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn.EditingServiceCreate;

/// <summary>
/// Property Values are read the way the Management API reads request values (the CMS's
/// <c>JsonObjectConverter</c>), so each property editor's <c>FromEditor</c> gets the CLR shape it
/// gets from the backoffice. Stored-format values for four pickers are rewritten first.
/// </summary>
public class PropertyValuesInEditorFormatTests
{
    public class GivenJsonValuesOfEachKind
    {
        private readonly CreateContentHarness _harness = new CreateContentHarness()
            .WithProperty("title")
            .WithProperty("count", Constants.PropertyEditors.Aliases.Integer)
            .WithProperty("featured", Constants.PropertyEditors.Aliases.Boolean)
            .WithProperty("tags", Constants.PropertyEditors.Aliases.Tags)
            .WithProperty("blocks", Constants.PropertyEditors.Aliases.BlockList)
            .WithProperty("links", Constants.PropertyEditors.Aliases.MultiUrlPicker);

        private const string Json = """
            {
              "title": "Hello",
              "count": 42,
              "featured": true,
              "tags": ["a", "b"],
              "blocks": { "layout": {} },
              "links": [{ "name": "Home", "type": "external", "url": "https://example.com" }]
            }
            """;

        [Fact(Skip = "Pending")]
        public async Task AStringStaysAString()
        {
            await _harness.RunAsync(_harness.Settings(Json));

            _harness.Value("title").ShouldNotBeNull().Value.ShouldBe("Hello");
        }

        [Fact(Skip = "Pending")]
        public async Task AWholeNumberBecomesAnInt()
        {
            await _harness.RunAsync(_harness.Settings(Json));

            _harness.Value("count").ShouldNotBeNull().Value.ShouldBe(42);
        }

        [Fact(Skip = "Pending")]
        public async Task ABooleanStaysABoolean()
        {
            await _harness.RunAsync(_harness.Settings(Json));

            _harness.Value("featured").ShouldNotBeNull().Value.ShouldBe(true);
        }

        [Fact(Skip = "Pending")]
        public async Task AnArrayOfStringsBecomesAListOfStrings()
        {
            await _harness.RunAsync(_harness.Settings(Json));

            (_harness.Value("tags")?.Value as IEnumerable<string>).ShouldBe(["a", "b"]);
        }

        [Fact(Skip = "Pending")]
        public async Task AnObjectBecomesAJsonObject()
        {
            await _harness.RunAsync(_harness.Settings(Json));

            _harness.Value("blocks").ShouldNotBeNull().Value.ShouldBeOfType<JsonObject>();
        }

        [Fact(Skip = "Pending")]
        public async Task AnArrayOfObjectsBecomesAJsonArray()
        {
            await _harness.RunAsync(_harness.Settings(Json));

            _harness.Value("links").ShouldNotBeNull().Value.ShouldBeOfType<JsonArray>();
        }
    }

    public class GivenACultureVariantType
    {
        private readonly CreateContentHarness _harness = new CreateContentHarness(ContentVariation.Culture)
            .WithProperty("title", variations: ContentVariation.Culture)
            .WithProperty("code");

        private const string Json = """{"title": "Hello", "code": "X1"}""";

        [Fact(Skip = "Pending")]
        public async Task AVariantPropertyCarriesTheStepsCulture()
        {
            await _harness.RunAsync(_harness.Settings(Json, culture: "en-US"));

            _harness.Value("title").ShouldNotBeNull().Culture.ShouldBe("en-US");
        }

        [Fact(Skip = "Pending")]
        public async Task AnInvariantPropertyCarriesNoCulture()
        {
            await _harness.RunAsync(_harness.Settings(Json, culture: "en-US"));

            _harness.Value("code").ShouldNotBeNull().Culture.ShouldBeNull();
        }
    }

    public class GivenCreateMedia
    {
        private readonly CreateMediaHarness _harness = new CreateMediaHarness().WithProperty("altText");

        [Fact(Skip = "Pending")]
        public async Task ReadsValuesTheSameWay()
        {
            await _harness.RunAsync(_harness.Settings(propertiesJson: """{"altText": "Sunset"}"""));

            _harness.Value("altText").ShouldNotBeNull().Value.ShouldBe("Sunset");
        }
    }

    public class GivenPickerValuesInTheStoredFormat
    {
        private readonly Guid _key = Guid.NewGuid();

        private readonly CreateContentHarness _harness = new CreateContentHarness()
            .WithProperty("link", Constants.PropertyEditors.Aliases.ContentPicker)
            .WithProperty("related", Constants.PropertyEditors.Aliases.MultiNodeTreePicker)
            .WithProperty("cta", Constants.PropertyEditors.Aliases.MultiUrlPicker);

        private string Json => $$"""
            {
              "link": "umb://document/{{_key:N}}",
              "related": "umb://document/{{_key:N}}",
              "cta": [{ "name": "Home", "udi": "umb://document/{{_key:N}}" }]
            }
            """;

        [Fact(Skip = "Pending")]
        public async Task AContentPickerUdiBecomesAKey()
        {
            await _harness.RunAsync(_harness.Settings(Json));

            _harness.Value("link").ShouldNotBeNull().Value.ShouldBe(_key.ToString());
        }

        [Fact(Skip = "Pending")]
        public async Task ATreePickerUdiListBecomesEntityReferences()
        {
            await _harness.RunAsync(_harness.Settings(Json));

            CreateActionSpecs.Json(_harness.Value("related")?.Value)
                .ShouldBe($$"""[{"type":"document","unique":"{{_key}}"}]""");
        }

        [Fact(Skip = "Pending")]
        public async Task AUrlPickerUdiLinkGetsTypeAndUnique()
        {
            await _harness.RunAsync(_harness.Settings(Json));

            CreateActionSpecs.JsonField((_harness.Value("cta")?.Value as JsonArray)?[0], "unique").ShouldBe(_key.ToString());
        }
    }

    public class GivenValuesThatAreNotSent
    {
        private readonly CreateContentHarness _harness = new CreateContentHarness().WithProperty("title");

        [Fact(Skip = "Pending")]
        public async Task AnUnknownAliasIsNotSent()
        {
            await _harness.RunAsync(_harness.Settings("""{"title": "Hello", "nope": "x"}"""));

            _harness.Value("nope").ShouldBeNull();
        }

        [Fact(Skip = "Pending")]
        public async Task ANullValueIsNotSent()
        {
            await _harness.RunAsync(_harness.Settings("""{"title": null}"""));

            _harness.Value("title").ShouldBeNull();
        }

        [Fact(Skip = "Pending")]
        public async Task MalformedJsonSendsNoValues()
        {
            await _harness.RunAsync(_harness.Settings("{not json"));

            _harness.Model.ShouldNotBeNull().Properties.ShouldBeEmpty();
        }
    }
}
