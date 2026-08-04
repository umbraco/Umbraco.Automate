using Shouldly;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Settings;

public class EditableModelSchemaBuilderTests
{
    private const string SensitiveFieldAlias = "Umb.Automate.SensitiveField";

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
        apiKeyField.Group.ShouldBe("#uaFieldGroups_authenticationLabel");
    }

    [Fact]
    public void Build_UsesLocalizationKey_WhenNoLabel()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(SampleSettings))!;

        var field = schema.Fields.First(f => f.PropertyName == "EndpointUrl");
        field.Label.ShouldBe("#uaFields_sampleSettingsEndpointUrlLabel");
        field.Description.ShouldBe("#uaFields_sampleSettingsEndpointUrlDescription");
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
    public void Build_PropagatesSupportsBindings()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(BindingSettings))!;

        var markedField = schema.Fields.First(f => f.PropertyName == "Marked");
        markedField.SupportsBindings.ShouldBeTrue();

        var unmarkedField = schema.Fields.First(f => f.PropertyName == "Unmarked");
        unmarkedField.SupportsBindings.ShouldBeFalse();

        var noAttrField = schema.Fields.First(f => f.PropertyName == "NoAttribute");
        noAttrField.SupportsBindings.ShouldBeFalse();
    }

    [Fact]
    public void Build_CachesByType_ReturningSameInstance()
    {
        // Use a type that no other test caches, to keep this test independent.
        var first = EditableModelSchemaBuilder.Build(typeof(CacheProbeSettings));
        var second = EditableModelSchemaBuilder.Build(typeof(CacheProbeSettings));

        first.ShouldBeSameAs(second);
    }

    [Theory]
    [InlineData(nameof(EditorInferenceSettings.StringField), "Umb.PropertyEditorUi.TextBox")]
    [InlineData(nameof(EditorInferenceSettings.NullableStringField), "Umb.PropertyEditorUi.TextBox")]
    [InlineData(nameof(EditorInferenceSettings.IntField), "Umb.PropertyEditorUi.Integer")]
    [InlineData(nameof(EditorInferenceSettings.NullableIntField), "Umb.PropertyEditorUi.Integer")]
    [InlineData(nameof(EditorInferenceSettings.LongField), "Umb.PropertyEditorUi.Integer")]
    [InlineData(nameof(EditorInferenceSettings.NullableLongField), "Umb.PropertyEditorUi.Integer")]
    [InlineData(nameof(EditorInferenceSettings.BoolField), "Umb.PropertyEditorUi.Toggle")]
    [InlineData(nameof(EditorInferenceSettings.NullableBoolField), "Umb.PropertyEditorUi.Toggle")]
    [InlineData(nameof(EditorInferenceSettings.DecimalField), "Umb.PropertyEditorUi.Decimal")]
    [InlineData(nameof(EditorInferenceSettings.NullableDecimalField), "Umb.PropertyEditorUi.Decimal")]
    [InlineData(nameof(EditorInferenceSettings.DoubleField), "Umb.PropertyEditorUi.Decimal")]
    [InlineData(nameof(EditorInferenceSettings.FloatField), "Umb.PropertyEditorUi.Decimal")]
    [InlineData(nameof(EditorInferenceSettings.DateTimeField), "Umb.PropertyEditorUi.DatePicker")]
    [InlineData(nameof(EditorInferenceSettings.NullableDateTimeField), "Umb.PropertyEditorUi.DatePicker")]
    [InlineData(nameof(EditorInferenceSettings.DateTimeOffsetField), "Umb.PropertyEditorUi.DatePicker")]
    [InlineData(nameof(EditorInferenceSettings.NullableDateTimeOffsetField), "Umb.PropertyEditorUi.DatePicker")]
    public void Build_InfersEditorUiAlias_FromPropertyType(string propertyName, string expectedAlias)
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(EditorInferenceSettings))!;

        var field = schema.Fields.First(f => f.PropertyName == propertyName);

        field.EditorUiAlias.ShouldBe(expectedAlias);
    }

    [Fact]
    public void Build_AttributeEditorUiAlias_WinsOverInference()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(EditorInferenceSettings))!;

        var field = schema.Fields.First(f => f.PropertyName == "Explicit");

        field.EditorUiAlias.ShouldBe("Umb.PropertyEditorUi.TextArea");
    }

    [Fact]
    public void Build_InfersMaskedEditor_ForSensitiveString()
    {
        // A credential should never render as a plain text box by default, so it isn't left
        // on screen during demos and screen shares.
        var schema = EditableModelSchemaBuilder.Build(typeof(SensitiveEditorSettings))!;

        var field = schema.Fields.First(f => f.PropertyName == "ApiKey");

        field.EditorUiAlias.ShouldBe(SensitiveFieldAlias);
    }

    [Fact]
    public void Build_InfersMaskedEditor_ForNullableSensitiveString()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(SensitiveEditorSettings))!;

        var field = schema.Fields.First(f => f.PropertyName == "NullableApiKey");

        field.EditorUiAlias.ShouldBe(SensitiveFieldAlias);
    }

    [Fact]
    public void Build_InfersTextBox_ForNonSensitiveString()
    {
        var schema = EditableModelSchemaBuilder.Build(typeof(SensitiveEditorSettings))!;

        var field = schema.Fields.First(f => f.PropertyName == "Endpoint");

        field.EditorUiAlias.ShouldBe("Umb.PropertyEditorUi.TextBox");
    }

    [Fact]
    public void Build_SensitiveWithExplicitEditorUiAlias_KeepsExplicitEditor()
    {
        // The explicit alias is the escape hatch: masking a structured field such as a JSON
        // headers blob would make it unusable, so a named editor always wins.
        var schema = EditableModelSchemaBuilder.Build(typeof(SensitiveEditorSettings))!;

        var field = schema.Fields.First(f => f.PropertyName == "HeadersJson");

        field.EditorUiAlias.ShouldBe("Umb.PropertyEditorUi.TextArea");
    }

    [Fact]
    public void Build_SensitiveNonStringType_KeepsTypeSpecificEditor()
    {
        // Masking only stands in for a text box. A type with its own editor keeps it.
        var schema = EditableModelSchemaBuilder.Build(typeof(SensitiveEditorSettings))!;

        var field = schema.Fields.First(f => f.PropertyName == "RotationDays");

        field.EditorUiAlias.ShouldBe("Umb.PropertyEditorUi.Integer");
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

    private class BindingSettings
    {
        [Field(SupportsBindings = true)]
        public string Marked { get; set; } = string.Empty;

        [Field]
        public string Unmarked { get; set; } = string.Empty;

        public string NoAttribute { get; set; } = string.Empty;
    }

    private class CacheProbeSettings
    {
        public string Anything { get; set; } = string.Empty;
    }

    private class SensitiveEditorSettings
    {
        [Field(IsSensitive = true)]
        public string ApiKey { get; set; } = string.Empty;

        [Field(IsSensitive = true)]
        public string? NullableApiKey { get; set; }

        [Field]
        public string Endpoint { get; set; } = string.Empty;

        [Field(IsSensitive = true, EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
        public string? HeadersJson { get; set; }

        [Field(IsSensitive = true)]
        public int RotationDays { get; set; }
    }

    private class EditorInferenceSettings
    {
        public string StringField { get; set; } = string.Empty;

        public string? NullableStringField { get; set; }

        public int IntField { get; set; }

        public int? NullableIntField { get; set; }

        public long LongField { get; set; }

        public long? NullableLongField { get; set; }

        public bool BoolField { get; set; }

        public bool? NullableBoolField { get; set; }

        public decimal DecimalField { get; set; }

        public decimal? NullableDecimalField { get; set; }

        public double DoubleField { get; set; }

        public float FloatField { get; set; }

        public DateTime DateTimeField { get; set; }

        public DateTime? NullableDateTimeField { get; set; }

        public DateTimeOffset DateTimeOffsetField { get; set; }

        public DateTimeOffset? NullableDateTimeOffsetField { get; set; }

        [Field(EditorUiAlias = "Umb.PropertyEditorUi.TextArea")]
        public string Explicit { get; set; } = string.Empty;
    }
}
