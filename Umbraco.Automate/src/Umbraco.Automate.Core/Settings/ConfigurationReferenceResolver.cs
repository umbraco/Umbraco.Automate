using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;

namespace Umbraco.Automate.Core.Settings;

/// <inheritdoc />
internal sealed class ConfigurationReferenceResolver : IConfigurationReferenceResolver
{
    private const char Sigil = '$';

    private readonly IConfiguration _configuration;
    private readonly IReadOnlyList<string> _allowedConfigKeyPrefixes;
    private readonly IReadOnlyList<string> _secretConfigKeyPrefixes;

    public ConfigurationReferenceResolver(IConfiguration configuration, IOptions<AutomateOptions>? options = null)
    {
        _configuration = configuration;

        // Ingest AutomateOptions exactly once for the whole "configuration reference" concept.
        // Both the serializer's skip-encryption decision and the resolver's substitution flow
        // delegate to this service, so the allow-list and how it is normalised live in one place
        // and the two callers cannot disagree. Fall back to the secure defaults (the
        // Secrets/Variables allow-list) when constructed without options; production always
        // supplies them via DI.
        var automateOptions = options?.Value ?? new AutomateOptions();
        _allowedConfigKeyPrefixes = Normalize(automateOptions.AllowedConfigurationKeyPrefixes);
        _secretConfigKeyPrefixes = Normalize(automateOptions.SecretConfigurationKeyPrefixes);
    }

    private static IReadOnlyList<string> Normalize(IEnumerable<string> prefixes) =>
        prefixes.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

    /// <inheritdoc />
    public bool ContainsReference(string? value) =>
        ConfigurationReferenceScanner.ContainsReference(value, _allowedConfigKeyPrefixes);

    /// <inheritdoc />
    public object? Resolve(object? value, Type targetType, bool isSensitiveField)
    {
        if (value is not string strValue || !strValue.Contains(Sigil))
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
        // password like "p$ssw0rd" — passes through untouched, and ${ ... } bindings are left
        // literal wherever they appear for the binding subsystem to resolve later. See
        // ConfigurationReferenceScanner.
        //
        // Limitation: the resolved value is spliced verbatim into the surrounding string. When
        // that string is later re-parsed as JSON (e.g. an HTTP Request action's Headers field),
        // a resolved value containing '"' or '\' can produce malformed JSON, which downstream
        // parsing may swallow silently. Structured (per-value) header modelling would avoid this
        // entirely — tracked under #161.
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
}
