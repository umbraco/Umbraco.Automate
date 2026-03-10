using Shouldly;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Settings;

public class FieldAttributeTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var attr = new FieldAttribute();

        attr.Label.ShouldBeNull();
        attr.Description.ShouldBeNull();
        attr.EditorUiAlias.ShouldBeNull();
        attr.EditorConfig.ShouldBeNull();
        attr.SortOrder.ShouldBe(0);
        attr.IsSensitive.ShouldBeFalse();
        attr.Group.ShouldBeNull();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var attr = new FieldAttribute
        {
            Label = "API Key",
            Description = "Your API key for authentication",
            EditorUiAlias = "Umb.PropertyEditorUi.TextBox",
            EditorConfig = """[{ "alias": "inputType", "value": "password" }]""",
            SortOrder = 10,
            IsSensitive = true,
            Group = "Authentication",
        };

        attr.Label.ShouldBe("API Key");
        attr.Description.ShouldBe("Your API key for authentication");
        attr.EditorUiAlias.ShouldBe("Umb.PropertyEditorUi.TextBox");
        attr.EditorConfig.ShouldNotBeNull();
        attr.SortOrder.ShouldBe(10);
        attr.IsSensitive.ShouldBeTrue();
        attr.Group.ShouldBe("Authentication");
    }

    [Fact]
    public void FieldAttribute_IsReadableViaReflection()
    {
        var property = typeof(SampleSettings).GetProperty(nameof(SampleSettings.ApiKey))!;
        var attr = property
            .GetCustomAttributes(typeof(EditableModelFieldAttribute), true)
            .Cast<EditableModelFieldAttribute>()
            .Single();

        attr.Label.ShouldBe("API Key");
        attr.IsSensitive.ShouldBeTrue();
        attr.SortOrder.ShouldBe(1);
    }

    [Fact]
    public void FieldAttribute_InheritsFromEditableModelFieldAttribute()
    {
        new FieldAttribute().ShouldBeAssignableTo<EditableModelFieldAttribute>();
    }

    private class SampleSettings
    {
        [Field(Label = "API Key", IsSensitive = true, SortOrder = 1)]
        public string ApiKey { get; set; } = string.Empty;

        [Field(Label = "Endpoint URL", Description = "The base URL", SortOrder = 2)]
        public string Url { get; set; } = string.Empty;
    }
}
