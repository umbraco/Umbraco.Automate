using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Settings;

/// <summary>
/// From CMS 19 a picker's minimum item count no longer makes it mandatory in the backoffice, so the
/// settings that need a pick are marked required and enforced by the settings validation that runs
/// when an automation is published and when a step runs.
/// </summary>
public class RequiredPickerSettingsTests
{
    public static TheoryData<Type, string> RequiredPickers => new()
    {
        { typeof(CreateContentSettings), nameof(CreateContentSettings.ContentType) },
        { typeof(CreateMediaSettings), nameof(CreateMediaSettings.MediaType) },
        { typeof(MoveContentSettings), nameof(MoveContentSettings.ContentKey) },
        { typeof(MoveMediaSettings), nameof(MoveMediaSettings.MediaKey) },
    };

    [Theory]
    [MemberData(nameof(RequiredPickers))]
    public void Picker_IsMarkedRequiredInTheSchema(Type settingsType, string propertyName)
    {
        var schema = EditableModelSchemaBuilder.Build(settingsType)!;

        schema.Fields.First(f => f.PropertyName == propertyName).IsRequired.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(RequiredPickers))]
    public void EmptyPicker_FailsSettingsValidation_OnItsOwnField(Type settingsType, string propertyName)
    {
        var schema = EditableModelSchemaBuilder.Build(settingsType)!;
        var settings = Activator.CreateInstance(settingsType)!;

        // Every other required field is filled in, so the only possible error is the picker's own.
        foreach (var other in schema.Fields.Where(f => f.IsRequired && f.PropertyName != propertyName))
        {
            settingsType.GetProperty(other.PropertyName!)!.SetValue(settings, "x");
        }

        var errors = EditableModelResolver.GetValidationErrors(settings, schema, _ => false);

        var label = schema.Fields.First(f => f.PropertyName == propertyName).Label;
        errors.Count.ShouldBe(1);
        errors[0].ShouldContain(label);
    }

    [Theory]
    [MemberData(nameof(RequiredPickers))]
    public void FilledPicker_PassesSettingsValidation(Type settingsType, string propertyName)
    {
        var schema = EditableModelSchemaBuilder.Build(settingsType)!;
        var settings = Activator.CreateInstance(settingsType)!;
        foreach (var field in schema.Fields.Where(f => f.IsRequired))
        {
            settingsType.GetProperty(field.PropertyName!)!.SetValue(settings, "x");
        }

        EditableModelResolver.GetValidationErrors(settings, schema, _ => false).ShouldBeEmpty();
    }
}
