using System.ComponentModel.DataAnnotations;
using System.Reflection;

using Umbraco.Automate.Extensions;

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

        // Create an instance to read default values from property initializers.
        var modelInstance = Activator.CreateInstance(settingsType);
        var modelKey = settingsType.Name.ToCamelCase();

        var fields = properties
            .Select(p => BuildFieldDescriptor(p, modelKey, modelInstance))
            .OrderBy(f => f.SortOrder)
            .ThenBy(f => f.Key)
            .ToList();

        return new EditableModelSchema { Type = settingsType, Fields = fields };
    }

    private static EditableModelFieldDescriptor BuildFieldDescriptor(
        PropertyInfo property, string modelKey, object? modelInstance)
    {
        var attr = property.GetCustomAttribute<EditableModelFieldAttribute>();
        var key = property.Name.ToCamelCase();
        var validationRules = InferValidationAttributes(property);

        // Read default value from the model instance's property initializer.
        object? defaultValue = null;
        if (modelInstance is not null && property.CanRead)
        {
            try
            {
                defaultValue = property.GetValue(modelInstance);
            }
            catch
            {
                // If we can't read the property value, leave it as null.
            }
        }

        return new EditableModelFieldDescriptor
        {
            Key = key,
            PropertyName = property.Name,
            Label = attr?.Label ?? $"#uaFields_{modelKey}{property.Name}Label",
            PropertyType = property.PropertyType,
            Description = attr?.Description ?? $"#uaFields_{modelKey}{property.Name}Description",
            EditorUiAlias = attr?.EditorUiAlias,
            EditorConfig = attr?.EditorConfig,
            DefaultValue = defaultValue,
            SortOrder = attr?.SortOrder ?? 0,
            IsSensitive = attr?.IsSensitive ?? false,
            IsRequired = validationRules.OfType<RequiredAttribute>().Any(),
            Group = attr?.Group is null or "" ? null :
                attr.Group.StartsWith('#') ? attr.Group : $"#uaFieldGroups_{attr.Group.ToCamelCase()}Label",
            SupportsBindings = attr?.SupportsBindings ?? false,
            ValidationRules = validationRules,
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
