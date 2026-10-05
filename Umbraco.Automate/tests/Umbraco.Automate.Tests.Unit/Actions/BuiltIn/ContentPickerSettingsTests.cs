using Shouldly;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Bindings.Filters;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class ContentPickerSettingsTests
{
    private const string DocumentPickerAlias = "Umb.PropertyEditorUi.DocumentPicker";
    private const string SingleRequiredConfig = """[{ "alias": "validationLimit", "value": { "min": 1, "max": 1 } }]""";

    private static readonly SettingsBindingResolver Resolver = new(
        new BindingEvaluator(new BindingFilterCollection(() => [])));

    private static readonly Dictionary<string, object?> NoData = new()
    {
        ["trigger"] = new Dictionary<string, object?>(),
    };

    public static TheoryData<Type> RequiredContentKeySettings => new()
    {
        typeof(PublishContentSettings),
        typeof(UnpublishContentActionSettings),
        typeof(GetContentSettings),
        typeof(GetContentPropertySettings),
        typeof(UpdateContentPropertySettings),
        typeof(NotifyEditorSettings),
        typeof(MoveContentSettings),
    };

    [Theory]
    [MemberData(nameof(RequiredContentKeySettings))]
    public void ContentKey_UsesDocumentPicker(Type settingsType)
        => Field(settingsType, "contentKey").EditorUiAlias.ShouldBe(DocumentPickerAlias);

    [Theory]
    [MemberData(nameof(RequiredContentKeySettings))]
    public void ContentKey_LimitsSelectionToExactlyOne(Type settingsType)
        => Field(settingsType, "contentKey").EditorConfig.ShouldBe(SingleRequiredConfig);

    [Theory]
    [MemberData(nameof(RequiredContentKeySettings))]
    public void ContentKey_SupportsBindings(Type settingsType)
        => Field(settingsType, "contentKey").SupportsBindings.ShouldBeTrue();

    [Theory]
    [MemberData(nameof(RequiredContentKeySettings))]
    public void ContentKey_IsStoredAsString(Type settingsType)
        => Field(settingsType, "contentKey").ValueKind.ShouldBe(EditableModelValueKind.String);

    [Fact]
    public void MoveContentTargetParentKey_SupportsBindings()
        => Field(typeof(MoveContentSettings), "targetParentKey").SupportsBindings.ShouldBeTrue();

    [Fact]
    public void CreateContentParentKey_SupportsBindings()
        => Field(typeof(CreateContentSettings), "parentKey").SupportsBindings.ShouldBeTrue();

    [Fact]
    public void MoveContentTargetParentKey_ThrowsWhenBindingResolvesToNothing()
    {
        var settings = new MoveContentSettings { TargetParentKey = "${ trigger.missing }" };

        Should.Throw<SettingsBindingException>(() => Resolver.ResolveBindings(settings, NoData));
    }

    [Fact]
    public void CreateContentParentKey_ThrowsWhenBindingResolvesToNothing()
    {
        var settings = new CreateContentSettings { ParentKey = "${ trigger.missing }" };

        Should.Throw<SettingsBindingException>(() => Resolver.ResolveBindings(settings, NoData));
    }

    [Fact]
    public void CreateContentParentKey_StaysEmptyWhenNoBindingIsSet()
    {
        var settings = new CreateContentSettings { ParentKey = null };

        Resolver.ResolveBindings(settings, NoData);

        settings.ParentKey.ShouldBeNull();
    }

    private static EditableModelFieldDescriptor Field(Type settingsType, string key)
        => EditableModelSchemaBuilder.Build(settingsType)!.Fields.Single(f => f.Key == key);
}
