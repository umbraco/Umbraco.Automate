using Shouldly;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Bindings;
using Umbraco.Automate.Core.Bindings.Filters;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn;

public class MediaPickerSettingsTests
{
    private const string FilesOnlyConfig =
        """[{ "alias": "validationLimit", "value": { "min": 1, "max": 1 } }, { "alias": "folderFilter", "value": "filesOnly" }]""";

    private const string FilesAndFoldersConfig =
        """[{ "alias": "validationLimit", "value": { "min": 1, "max": 1 } }, { "alias": "folderFilter", "value": "filesAndFolders" }]""";

    private static readonly SettingsBindingResolver Resolver = new(
        new BindingEvaluator(new BindingFilterCollection(() => [])));

    private static readonly Dictionary<string, object?> NoData = new()
    {
        ["trigger"] = new Dictionary<string, object?>(),
    };

    public static TheoryData<Type> FilesOnlyMediaKeySettings => new()
    {
        typeof(GetMediaSettings),
        typeof(GetMediaPropertySettings),
        typeof(UpdateMediaPropertySettings),
    };

    public static TheoryData<Type> AllMediaKeySettings => new()
    {
        typeof(GetMediaSettings),
        typeof(GetMediaPropertySettings),
        typeof(UpdateMediaPropertySettings),
        typeof(MoveMediaSettings),
    };

    [Theory]
    [MemberData(nameof(AllMediaKeySettings))]
    public void MediaKey_UsesMediaKeyPicker(Type settingsType)
        => Field(settingsType, "mediaKey").EditorUiAlias.ShouldBe(Constants.EditorUiAliases.MediaKeyPicker);

    [Theory]
    [MemberData(nameof(FilesOnlyMediaKeySettings))]
    public void MediaKey_LimitsSelectionToOneFile(Type settingsType)
        => Field(settingsType, "mediaKey").EditorConfig.ShouldBe(FilesOnlyConfig);

    [Fact]
    public void MoveMediaKey_LimitsSelectionToOneFileOrFolder()
        => Field(typeof(MoveMediaSettings), "mediaKey").EditorConfig.ShouldBe(FilesAndFoldersConfig);

    [Theory]
    [MemberData(nameof(AllMediaKeySettings))]
    public void MediaKey_SupportsBindings(Type settingsType)
        => Field(settingsType, "mediaKey").SupportsBindings.ShouldBeTrue();

    [Theory]
    [MemberData(nameof(AllMediaKeySettings))]
    public void MediaKey_IsStoredAsString(Type settingsType)
        => Field(settingsType, "mediaKey").ValueKind.ShouldBe(EditableModelValueKind.String);

    [Fact]
    public void CreateMediaParentKey_SupportsBindings()
        => Field(typeof(CreateMediaSettings), "parentKey").SupportsBindings.ShouldBeTrue();

    [Fact]
    public void MoveMediaTargetParentKey_SupportsBindings()
        => Field(typeof(MoveMediaSettings), "targetParentKey").SupportsBindings.ShouldBeTrue();

    [Fact]
    public void CreateMediaParentKey_ThrowsWhenBindingResolvesToNothing()
    {
        var settings = new CreateMediaSettings { ParentKey = "${ trigger.missing }" };

        Should.Throw<SettingsBindingException>(() => Resolver.ResolveBindings(settings, NoData));
    }

    [Fact]
    public void MoveMediaTargetParentKey_ThrowsWhenBindingResolvesToNothing()
    {
        var settings = new MoveMediaSettings { TargetParentKey = "${ trigger.missing }" };

        Should.Throw<SettingsBindingException>(() => Resolver.ResolveBindings(settings, NoData));
    }

    [Fact]
    public void CreateMediaParentKey_KeepsLiteralEmptyString()
    {
        var settings = new CreateMediaSettings { ParentKey = "" };

        Resolver.ResolveBindings(settings, NoData);

        settings.ParentKey.ShouldBe("");
    }

    [Fact]
    public void MoveMediaTargetParentKey_KeepsLiteralEmptyString()
    {
        var settings = new MoveMediaSettings { TargetParentKey = "" };

        Resolver.ResolveBindings(settings, NoData);

        settings.TargetParentKey.ShouldBe("");
    }

    private static EditableModelFieldDescriptor Field(Type settingsType, string key)
        => EditableModelSchemaBuilder.Build(settingsType)!.Fields.Single(f => f.Key == key);
}
