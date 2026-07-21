using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Service for resolving editable models from various storage formats.
/// Handles JSON deserialization, configuration variable substitution, and validation.
/// </summary>
/// <remarks>
/// Configuration substitution (<c>$Key:Path</c>) is default-deny: a key is only resolved
/// when it falls under one of <see cref="AutomateOptions.AllowedConfigurationKeyPrefixes"/>,
/// keeping resolution scoped to configuration explicitly intended for automations rather than
/// the whole configuration tree under the elevated run identity.
/// </remarks>
internal sealed class EditableModelResolver : IEditableModelResolver
{
    private const string ConfigPrefix = "$";

    private readonly IConfiguration _configuration;
    private readonly IReadOnlyList<string> _allowedConfigKeyPrefixes;
    private readonly IReadOnlyList<string> _secretConfigKeyPrefixes;

    public EditableModelResolver(IConfiguration configuration, IOptions<AutomateOptions>? options = null)
    {
        _configuration = configuration;

        // Fall back to defaults (the Secrets/Variables allow-list) when constructed without
        // options. Production always supplies them via DI; this keeps the default secure
        // rather than permissive.
        var automateOptions = options?.Value ?? new AutomateOptions();
        _allowedConfigKeyPrefixes = automateOptions.AllowedConfigurationKeyPrefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
        _secretConfigKeyPrefixes = automateOptions.SecretConfigurationKeyPrefixes
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToArray();
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
            var resolvedValue = ResolveConfigurationVariable(value, property.PropertyType, isSensitiveField);

            if (!Equals(value, resolvedValue))
            {
                property.SetValue(obj, resolvedValue);
            }
        }
    }

    private object? ResolveConfigurationVariable(object? value, Type targetType, bool isSensitiveField)
    {
        if (value is not string strValue || !strValue.Contains(ConfigPrefix, StringComparison.Ordinal))
        {
            return value;
        }

        // Skip whole-value binding expressions (${ ... }) — they are resolved later by
        // SettingsBindingResolver. Embedded bindings are left intact by the scanner below.
        if (strValue.StartsWith("${", StringComparison.Ordinal))
        {
            return value;
        }

        // Whole-value single reference: preserve the original gate chain (default-deny allow-list
        // that throws for out-of-scope keys) and type conversion for non-string targets.
        if (ConfigurationReferenceScanner.IsWholeReference(strValue, out var wholeKey))
        {
            if (!ConfigurationReferenceScanner.MatchesPrefix(wholeKey, _allowedConfigKeyPrefixes))
            {
                throw new InvalidOperationException(BuildNotPermittedMessage(wholeKey));
            }

            return ConvertToTargetType(LookupConfigValue(wholeKey, isSensitiveField), targetType);
        }

        // Otherwise scan for references embedded in a larger string and splice in their string
        // values. Tokens outside the allow-list are left literal (no throw) so a stray '$' — a
        // password like "p$ssw0rd" — passes through untouched. See ConfigurationReferenceScanner.
        var resolved = ConfigurationReferenceScanner.Scan(
            strValue,
            _allowedConfigKeyPrefixes,
            key => LookupConfigValue(key, isSensitiveField));

        return string.Equals(resolved, strValue, StringComparison.Ordinal) ? value : resolved;
    }

    /// <summary>
    /// Applies the secret-into-sensitive-only restriction and the config lookup shared by the
    /// whole-value and embedded resolution paths. The caller has already confirmed the key is
    /// under an allowed prefix.
    /// </summary>
    private string LookupConfigValue(string configKey, bool isSensitiveField)
    {
        // Secret keys may only resolve into sensitive fields, so a resolved secret stays in
        // fields the system treats as credential-bearing rather than ones whose values may be
        // surfaced in clear. See SecretConfigurationKeyPrefixes.
        if (!isSensitiveField && ConfigurationReferenceScanner.MatchesPrefix(configKey, _secretConfigKeyPrefixes))
        {
            throw new InvalidOperationException(
                $"Configuration key '{configKey}' is a secret and may only be referenced from " +
                $"a sensitive field (one marked [Field(IsSensitive = true)]). Move the value " +
                $"to a non-secret section (e.g. Umbraco:Automate:Variables) if it is safe to " +
                $"expose in this field, or reference it from a sensitive field instead.");
        }

        var configValue = _configuration[configKey];

        if (configValue is null)
        {
            throw new InvalidOperationException(
                $"Configuration key '{configKey}' not found. " +
                $"Ensure the key is set in appsettings.json, environment variables, or other configuration sources before using ${configKey} in settings.");
        }

        return configValue;
    }

    private string BuildNotPermittedMessage(string configKey) =>
        $"Configuration key '{configKey}' is not permitted in settings. " +
        $"Only keys under an allowed prefix may be referenced with the $ syntax " +
        $"(by default '{string.Join("', '", _allowedConfigKeyPrefixes)}'). " +
        $"An administrator can place the value under an allowed section or extend " +
        $"Umbraco:Automate:AllowedConfigurationKeyPrefixes in app settings.";

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
