using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Builds an <see cref="EditableModelSchema"/> from a settings POCO type
/// by reflecting over <see cref="EditableModelFieldAttribute"/>-decorated properties.
/// </summary>
public static class EditableModelSchemaBuilder
{
    /// <summary>
    /// Builds the schema from the given settings type.
    /// Properties without <see cref="EditableModelFieldAttribute"/> are included with defaults.
    /// </summary>
    /// <param name="settingsType">The settings POCO type.</param>
    /// <returns>The schema, or null if the type has no public properties.</returns>
    public static EditableModelSchema? Build(Type settingsType)
    {
        ArgumentNullException.ThrowIfNull(settingsType);

        var properties = settingsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        if (properties.Length == 0)
        {
            return null;
        }

        var fields = properties
            .Select(BuildFieldDescriptor)
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.PropertyName)
            .ToList();

        return new EditableModelSchema { Fields = fields };
    }

    private static EditableModelFieldDescriptor BuildFieldDescriptor(PropertyInfo property)
    {
        var attr = property.GetCustomAttribute<EditableModelFieldAttribute>();

        return new EditableModelFieldDescriptor
        {
            PropertyName = property.Name,
            Label = attr?.Label ?? HumanizePropertyName(property.Name),
            PropertyType = property.PropertyType,
            Description = attr?.Description,
            EditorUiAlias = attr?.EditorUiAlias,
            EditorConfig = attr?.EditorConfig,
            SortOrder = attr?.SortOrder ?? 0,
            IsSensitive = attr?.IsSensitive ?? false,
            Group = attr?.Group,
            SupportsBindings = attr?.SupportsBindings ?? false,
            ValidationRules = InferValidationAttributes(property),
        };
    }

    private static IEnumerable<ValidationAttribute> InferValidationAttributes(PropertyInfo property)
    {
        var validationAttributes = property.GetCustomAttributes<ValidationAttribute>().ToList();

        // If the property is a non-nullable reference type and doesn't already have a
        // Required attribute, add one. Value types always have a default value so skip them.
        var nullabilityContext = new NullabilityInfoContext();
        var nullabilityInfo = nullabilityContext.Create(property);

        if (nullabilityInfo.WriteState != NullabilityState.Nullable
            && !property.PropertyType.IsValueType
            && !validationAttributes.OfType<RequiredAttribute>().Any())
        {
            validationAttributes.Add(new RequiredAttribute());
        }

        return validationAttributes;
    }

    /// <summary>
    /// Converts a PascalCase property name to a human-readable label
    /// (e.g. "ContentName" → "Content Name").
    /// </summary>
    internal static string HumanizePropertyName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return name;
        }

        var chars = new List<char>(name.Length + 4) { name[0] };
        for (var i = 1; i < name.Length; i++)
        {
            if (char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
            {
                chars.Add(' ');
            }

            chars.Add(name[i]);
        }

        return new string(chars.ToArray());
    }
}
