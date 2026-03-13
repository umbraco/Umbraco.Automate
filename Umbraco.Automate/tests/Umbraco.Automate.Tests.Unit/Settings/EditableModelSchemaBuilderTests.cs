using Shouldly;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Settings;

public class EditableModelSchemaBuilderTests
{
    [Fact]
    public void Build_ReturnsNull_ForEmptyType()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(EmptySettings));

        schema.ShouldBeNull();
    }

    [Fact]
    public void Build_IncludesAllPublicProperties()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(SampleSettings));

        schema.ShouldNotBeNull();
        schema.Fields.Count.ShouldBe(3);
    }

    [Fact]
    public void Build_ReadsFieldAttributeMetadata()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(SampleSettings))!;

        var apiKeyField = schema.Fields.First(f => f.PropertyName == "ApiKey");
        apiKeyField.Label.ShouldBe("API Key");
        apiKeyField.Description.ShouldBe("Your secret key");
        apiKeyField.IsSensitive.ShouldBeTrue();
        apiKeyField.Group.ShouldBe("Authentication");
    }

    [Fact]
    public void Build_UsesHumanizedName_WhenNoLabel()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(SampleSettings))!;

        var field = schema.Fields.First(f => f.PropertyName == "EndpointUrl");
        field.Label.ShouldBe("Endpoint Url");
    }

    [Fact]
    public void Build_OrdersBySortOrderThenPropertyName()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(SampleSettings))!;

        schema.Fields[0].PropertyName.ShouldBe("EndpointUrl");
        schema.Fields[1].PropertyName.ShouldBe("ApiKey");
        schema.Fields[2].PropertyName.ShouldBe("Timeout");
    }

    [Fact]
    public void Build_PropagatesSupportsExpressions()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(ExpressionSettings))!;

        var markedField = schema.Fields.First(f => f.PropertyName == "Marked");
        markedField.SupportsExpressions.ShouldBeTrue();

        var unmarkedField = schema.Fields.First(f => f.PropertyName == "Unmarked");
        unmarkedField.SupportsExpressions.ShouldBeFalse();

        var noAttrField = schema.Fields.First(f => f.PropertyName == "NoAttribute");
        noAttrField.SupportsExpressions.ShouldBeFalse();
    }

    [Fact]
    public void HumanizePropertyName_ConvertsCorrectly()
    {
        EditableModelSchemaBuilder.HumanizePropertyName("ContentName").ShouldBe("Content Name");
        EditableModelSchemaBuilder.HumanizePropertyName("URL").ShouldBe("URL");
        EditableModelSchemaBuilder.HumanizePropertyName("A").ShouldBe("A");
        EditableModelSchemaBuilder.HumanizePropertyName("").ShouldBe("");
    }

    private class EmptySettings;

    private class SampleSettings
    {
        [Field(Label = "API Key", Description = "Your secret key", IsSensitive = true, SortOrder = 1, Group = "Authentication")]
        public string ApiKey { get; set; } = string.Empty;

        public string EndpointUrl { get; set; } = string.Empty;

        [Field(SortOrder = 2)]
        public int Timeout { get; set; }
    }

    private class ExpressionSettings
    {
        [Field(SupportsExpressions = true)]
        public string Marked { get; set; } = string.Empty;

        [Field]
        public string Unmarked { get; set; } = string.Empty;

        public string NoAttribute { get; set; } = string.Empty;
    }
}
