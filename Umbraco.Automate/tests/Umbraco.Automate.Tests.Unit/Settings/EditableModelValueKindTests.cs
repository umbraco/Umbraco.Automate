// STORY-1: The server says whether a field holds a string, another value or a collection (docs/plans/bindable-settings)
using Shouldly;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Conditions;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Settings;

public class EditableModelValueKindTests
{
    private const string Pending = "Pending: bindable-settings T2 adds EditableModelFieldDescriptor.ValueKind";

    // Pending specs must compile before ValueKind exists, so they read it by reflection.
    // T2 replaces this helper with `field.ValueKind` and compares against the enum.
    private static string? ValueKindOf(string propertyName)
    {
        var field = EditableModelSchemaBuilder.Build(typeof(ValueKindSettings))!.Fields
            .First(f => f.PropertyName == propertyName);
        return field.GetType().GetProperty("ValueKind")?.GetValue(field)?.ToString();
    }

    public class GivenAStringProperty
    {
        [Fact(Skip = Pending)]
        public void StringIsString() => ValueKindOf(nameof(ValueKindSettings.Text)).ShouldBe("String");
    }

    public class GivenAnotherSingleValueProperty
    {
        [Fact(Skip = Pending)]
        public void NullableGuidIsScalar() => ValueKindOf(nameof(ValueKindSettings.Key)).ShouldBe("Scalar");

        [Fact(Skip = Pending)]
        public void EnumIsScalar() => ValueKindOf(nameof(ValueKindSettings.Mode)).ShouldBe("Scalar");

        [Fact(Skip = Pending)]
        public void ObjectIsScalar() => ValueKindOf(nameof(ValueKindSettings.Conditions)).ShouldBe("Scalar");
    }

    public class GivenACollectionProperty
    {
        [Fact(Skip = Pending)]
        public void ListOfStringsIsCollection() => ValueKindOf(nameof(ValueKindSettings.Tags)).ShouldBe("Collection");

        [Fact(Skip = Pending)]
        public void ArrayIsCollection() => ValueKindOf(nameof(ValueKindSettings.Names)).ShouldBe("Collection");

        [Fact(Skip = Pending)]
        public void ListOfRowsIsCollection() => ValueKindOf(nameof(ValueKindSettings.Rows)).ShouldBe("Collection");
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
