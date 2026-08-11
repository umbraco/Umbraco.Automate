using System.Collections.Concurrent;
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
    // The schema for a given Type is deterministic and immutable across the process
    // lifetime, but building it requires reflection plus an Activator.CreateInstance
    // to read default values. Cache by Type so hot paths (deploy export, UI rendering,
    // settings validation, sensitive-field stripping) only pay that cost once.
    private static readonly ConcurrentDictionary<Type, EditableModelSchema?> Cache = new();

    /// <summary>
    /// Builds the schema from the given settings type.
    /// Properties without <see cref="EditableModelFieldAttribute"/> are included with defaults.
    /// Results are cached per <see cref="Type"/>.
    /// </summary>
    /// <param name="settingsType">The settings POCO type.</param>
    /// <returns>The schema, or null if the type has no public properties.</returns>
    public static EditableModelSchema? Build(Type settingsType)
    {
        ArgumentNullException.ThrowIfNull(settingsType);
        return Cache.GetOrAdd(settingsType, BuildUncached);
    }

    private static EditableModelSchema? BuildUncached(Type settingsType)
    {
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
            EditorUiAlias = attr?.EditorUiAlias ?? InferEditorUiAlias(property.PropertyType, attr?.IsSensitive ?? false),
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

    /// <summary>
    /// Picks a default Umbraco property editor UI alias from the CLR type so settings
    /// without an explicit <see cref="EditableModelFieldAttribute.EditorUiAlias"/> still
    /// render the right editor (numeric fields get a number input, booleans get a toggle,
    /// dates get a date picker, sensitive strings get a masked editor). The attribute
    /// always wins when set.
    /// </summary>
    /// <remarks>
    /// Masking is decided here rather than flagged to the client so the rule lives in one
    /// testable place and rendering never has to read <c>IsSensitive</c> — the same approach
    /// already taken for <c>IsRequired</c>. A settings author who wants a sensitive field
    /// rendered some other way sets <see cref="EditableModelFieldAttribute.EditorUiAlias"/>,
    /// which the caller checks first and so always wins; that is the escape hatch for a
    /// sensitive field masking would make unusable, such as the JSON headers on
    /// <c>HttpRequestSettings</c>.
    /// </remarks>
    private static string InferEditorUiAlias(Type type, bool isSensitive)
    {
        // Registered by the frontend static assets. Kept in step with SENSITIVE_FIELD_UI_ALIAS
        // in Client/src/core/components/sensitive-field/manifests.ts.
        const string sensitiveFieldAlias = "Umb.Automate.SensitiveField";

        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(string))
        {
            return isSensitive ? sensitiveFieldAlias : "Umb.PropertyEditorUi.TextBox";
        }

        if (underlyingType == typeof(int) || underlyingType == typeof(long))
        {
            return "Umb.PropertyEditorUi.Integer";
        }

        if (underlyingType == typeof(bool))
        {
            return "Umb.PropertyEditorUi.Toggle";
        }

        if (underlyingType == typeof(decimal) || underlyingType == typeof(double) || underlyingType == typeof(float))
        {
            return "Umb.PropertyEditorUi.Decimal";
        }

        if (underlyingType == typeof(DateTime) || underlyingType == typeof(DateTimeOffset))
        {
            return "Umb.PropertyEditorUi.DatePicker";
        }

        // Anything else falls back to a text box, so keep the sensitive pairing here too.
        return isSensitive ? sensitiveFieldAlias : "Umb.PropertyEditorUi.TextBox";
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
