using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Service for resolving editable models from various storage formats.
/// Handles JSON deserialization, configuration variable substitution, and validation.
/// </summary>
internal sealed class EditableModelResolver : IEditableModelResolver
{
    private const string ConfigPrefix = "$";

    private readonly ActionCollection _actions;
    private readonly TriggerCollection _triggers;
    private readonly IConfiguration _configuration;

    public EditableModelResolver(
        ActionCollection actions,
        TriggerCollection triggers,
        IConfiguration configuration)
    {
        _actions = actions;
        _triggers = triggers;
        _configuration = configuration;
    }

    /// <inheritdoc />
    public TModel? ResolveModel<TModel>(string modelId, object? data)
        where TModel : class, new()
        => (TModel?)ResolveModel(modelId, typeof(TModel), data);

    /// <inheritdoc />
    public object? ResolveModel(string modelId, Type modelType, object? data)
    {
        if (data is null)
        {
            return null;
        }

        // If already the correct type, clone via JSON round-trip to avoid mutating the original object.
        if (modelType.IsInstanceOfType(data))
        {
            var json = JsonSerializer.Serialize(data, JsonOptions.Default);
            var deserialized = JsonSerializer.Deserialize(json, modelType, JsonOptions.Default);
            if (deserialized is not null)
            {
                ResolveConfigurationVariablesInObject(deserialized);
                ValidateModel(modelId, deserialized);
            }

            return deserialized;
        }

        // Handle JsonElement deserialization.
        if (data is JsonElement jsonElement)
        {
            var deserialized = jsonElement.Deserialize(modelType, JsonOptions.Default);
            if (deserialized is not null)
            {
                ResolveConfigurationVariablesInObject(deserialized);
                ValidateModel(modelId, deserialized);
            }

            return deserialized;
        }

        // Try to serialize/deserialize through JSON as fallback.
        try
        {
            var json = JsonSerializer.Serialize(data, JsonOptions.Default);
            var deserialized = JsonSerializer.Deserialize(json, modelType, JsonOptions.Default);
            if (deserialized is not null)
            {
                ResolveConfigurationVariablesInObject(deserialized);
                ValidateModel(modelId, deserialized);
            }

            return deserialized;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to resolve model '{modelId}' to type {modelType.Name}",
                ex);
        }
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

            var value = property.GetValue(obj);
            var resolvedValue = ResolveConfigurationVariable(value, property.PropertyType);

            if (!Equals(value, resolvedValue))
            {
                property.SetValue(obj, resolvedValue);
            }
        }
    }

    private object? ResolveConfigurationVariable(object? value, Type targetType)
    {
        if (value is not string strValue || !strValue.StartsWith(ConfigPrefix))
        {
            return value;
        }

        var configKey = strValue[ConfigPrefix.Length..];
        var configValue = _configuration[configKey];

        if (configValue is null)
        {
            throw new InvalidOperationException(
                $"Configuration key '{configKey}' not found. " +
                $"Ensure the key is set in appsettings.json, environment variables, or other configuration sources before using ${configKey} in settings.");
        }

        return ConvertToTargetType(configValue, targetType);
    }

    private static object ConvertToTargetType(string value, Type targetType)
    {
        var underlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (underlyingType == typeof(string))
        {
            return value;
        }

        if (underlyingType == typeof(bool))
        {
            if (bool.TryParse(value, out var boolValue))
            {
                return boolValue;
            }

            throw new InvalidOperationException(
                $"Cannot convert configuration value '{value}' to boolean.");
        }

        if (underlyingType == typeof(int))
        {
            if (int.TryParse(value, out var intValue))
            {
                return intValue;
            }

            throw new InvalidOperationException(
                $"Cannot convert configuration value '{value}' to integer.");
        }

        if (underlyingType == typeof(long))
        {
            return long.Parse(value);
        }

        if (underlyingType == typeof(double))
        {
            return double.Parse(value);
        }

        if (underlyingType == typeof(decimal))
        {
            return decimal.Parse(value);
        }

        return value;
    }

    private void ValidateModel(string modelId, object model)
    {
        var schema = GetSettingsSchema(modelId);
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

    private EditableModelSchema? GetSettingsSchema(string modelId)
    {
        // Try actions first, then triggers.
        var action = _actions.FirstOrDefault(a => a.Alias == modelId);
        if (action is not null)
        {
            return action.GetSettingsSchema();
        }

        var trigger = _triggers.FirstOrDefault(t => t.Alias == modelId);
        return trigger?.GetSettingsSchema();
    }
}
