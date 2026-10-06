// STORY-1: The server says whether a field holds a string, another value or a collection (docs/plans/bindable-settings)
using Shouldly;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Settings;

public class EditableModelValueKindTests
{
    private static EditableModelValueKind ValueKindOf(string propertyName)
        => EditableModelSchemaBuilder.Build(typeof(ValueKindSettings))!.Fields
            .First(f => f.PropertyName == propertyName).ValueKind;

    public class GivenAStringProperty
    {
        [Fact]
        public void StringIsString() => ValueKindOf(nameof(ValueKindSettings.Text)).ShouldBe(EditableModelValueKind.String);
    }

    public class GivenAnotherSingleValueProperty
    {
        [Fact]
        public void NullableGuidIsScalar() => ValueKindOf(nameof(ValueKindSettings.Key)).ShouldBe(EditableModelValueKind.Scalar);

        [Fact]
        public void EnumIsScalar() => ValueKindOf(nameof(ValueKindSettings.Mode)).ShouldBe(EditableModelValueKind.Scalar);

        [Fact]
        public void ObjectIsScalar() => ValueKindOf(nameof(ValueKindSettings.Conditions)).ShouldBe(EditableModelValueKind.Scalar);
    }

    public class GivenACollectionProperty
    {
        [Fact]
        public void ListOfStringsIsCollection() => ValueKindOf(nameof(ValueKindSettings.Tags)).ShouldBe(EditableModelValueKind.Collection);

        [Fact]
        public void ArrayIsCollection() => ValueKindOf(nameof(ValueKindSettings.Names)).ShouldBe(EditableModelValueKind.Collection);

        [Fact]
        public void ListOfRowsIsCollection() => ValueKindOf(nameof(ValueKindSettings.Rows)).ShouldBe(EditableModelValueKind.Collection);
    }

    private enum ValueKindMode
    {
        First,
        Second,
    }

    private class ValueKindSettings
    {
        public string Text { get; set; } = string.Empty;

        public Guid? Key { get; set; }

        public ValueKindMode Mode { get; set; }

        public ConditionSet? Conditions { get; set; }

        public List<string> Tags { get; set; } = [];

        public string[] Names { get; set; } = [];

        public List<HttpRequestKeyValue> Rows { get; set; } = [];
    }
}
