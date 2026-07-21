using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Service for resolving editable models from various storage formats.
/// Handles JSON deserialization, configuration variable substitution, and validation.
/// </summary>
/// <remarks>
/// Configuration substitution (<c>$Key:Path</c>) is delegated to
/// <see cref="IConfigurationReferenceResolver"/>, which owns the allow-list policy and the
/// scanning algorithm so this resolver and <see cref="EditableModelSerializer"/> cannot drift.
/// </remarks>
internal sealed class EditableModelResolver : IEditableModelResolver
{
    private readonly IConfigurationReferenceResolver _configReferenceResolver;

    public EditableModelResolver(IConfigurationReferenceResolver configReferenceResolver)
    {
        _configReferenceResolver = configReferenceResolver;
    }

    /// <inheritdoc />
    public TModel? ResolveModel<TModel>(string modelId, object? data, EditableModelSchema? schema = null)
        where TModel : class, new()
        => (TModel?)ResolveModel(modelId, typeof(TModel), data, schema);

    /// <inheritdoc />
    public object? ResolveModel(string modelId, Type modelType, object? data, EditableModelSchema? schema = null)
    {
        if (data is null)
        {
            return null;
        }

        // If already the correct type, clone via JSON round-trip to avoid mutating the original object.
        if (modelType.IsInstanceOfType(data))
        {
            var json = JsonSerializer.Serialize(data, JsonOptions.Settings);
            var deserialized = JsonSerializer.Deserialize(json, modelType, JsonOptions.Settings);
            if (deserialized is not null)
            {
                ResolveConfigurationVariablesInObject(deserialized);
                ValidateModel(modelId, deserialized, schema);
            }

            return deserialized;
        }

        // Handle JsonElement deserialization.
        if (data is JsonElement jsonElement)
        {
            var deserialized = jsonElement.Deserialize(modelType, JsonOptions.Settings);
            if (deserialized is not null)
            {
                ResolveConfigurationVariablesInObject(deserialized);
                ValidateModel(modelId, deserialized, schema);
            }

            return deserialized;
        }

        // Try to serialize/deserialize through JSON as fallback.
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions.Settings);
            var deserialized = JsonSerializer.Deserialize(json, modelType, JsonOptions.Settings);
            if (deserialized is not null)
            {
                ResolveConfigurationVariablesInObject(deserialized);
                ValidateModel(modelId, deserialized, schema);
            }

            return deserialized;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                BuildResolveFailureMessage(modelId, modelType, ex),
                ex);
        }
    }

    /// <summary>
    /// Builds a diagnostic message naming the model, the inner failure type and message,
    /// and (for JSON failures) the JSON path of the offending property — without this,
    /// callers see only "Failed to resolve model ..." with no hint at the broken field.
    /// </summary>
    private static string BuildResolveFailureMessage(string modelId, Type modelType, Exception ex)
    {
        var path = (ex as JsonException)?.Path;

        var details = string.IsNullOrEmpty(path)
            ? $"{ex.GetType().Name}: {ex.Message}"
            : $"{ex.GetType().Name} at '{path}': {ex.Message}";

        return $"Failed to resolve model '{modelId}' to type {modelType.Name}. {details}";
    }

    private void ResolveConfigurationVariablesInObject(object obj)
    {
        var type = obj.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (!property.CanRead || !property.CanWrite)
            {
                continue;
            }

            // Read the field's sensitivity from its attribute (FieldAttribute derives from
            // EditableModelFieldAttribute), so secret config keys can be restricted to
            // sensitive fields without depending on the optional schema.
            var isSensitiveField = property
                .GetCustomAttribute<EditableModelFieldAttribute>()?.IsSensitive ?? false;

            var value = property.GetValue(obj);
            var resolvedValue = _configReferenceResolver.Resolve(value, property.PropertyType, isSensitiveField);

            if (!Equals(value, resolvedValue))
            {
                property.SetValue(obj, resolvedValue);
            }
        }
    }

    private static void ValidateModel(string modelId, object model, EditableModelSchema? schema)
    {
        if (schema is null)
        {
            return;
        }

        var modelType = model.GetType();
        var validationErrors = new List<string>();

        foreach (var field in schema.Fields)
        {
            if (string.IsNullOrEmpty(field.PropertyName))
            {
                continue;
            }

            var property = modelType.GetProperty(field.PropertyName);
            if (property is null)
            {
                continue;
            }

            var value = property.GetValue(model);

            foreach (var validationRule in field.ValidationRules)
            {
                var validationContext = new ValidationContext(model)
                {
                    MemberName = field.PropertyName,
                    DisplayName = field.Label,
                };

                var validationResult = validationRule.GetValidationResult(value, validationContext);
                if (validationResult != ValidationResult.Success)
                {
                    validationErrors.Add(validationResult?.ErrorMessage ?? $"Validation failed for {field.Label}");
                }
            }
        }

        if (validationErrors.Count > 0)
        {
            var errorMessage = $"Validation failed for model '{modelId}':\n" +
                               string.Join("\n", validationErrors);
            throw new InvalidOperationException(errorMessage);
        }
    }
}
