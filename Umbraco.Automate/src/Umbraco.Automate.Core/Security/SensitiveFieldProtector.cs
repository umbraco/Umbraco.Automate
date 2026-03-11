using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// Implements field protection using ASP.NET Core Data Protection API.
/// Uses a marker-based approach with <c>ENC:</c> prefix for encrypted values.
/// </summary>
internal sealed class SensitiveFieldProtector : ISensitiveFieldProtector
{
    private const string EncryptedPrefix = "ENC:";
    private const string Purpose = "Umbraco.Automate.SensitiveFields.v1";

    private readonly IDataProtector _protector;
    private readonly ILogger<SensitiveFieldProtector> _logger;

    public SensitiveFieldProtector(
        IDataProtectionProvider dataProtectionProvider,
        ILogger<SensitiveFieldProtector> logger)
    {
        _protector = dataProtectionProvider.CreateProtector(Purpose);
        _logger = logger;
    }

    /// <inheritdoc />
    public string? Protect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (IsProtected(value))
        {
            return value;
        }

        try
        {
            var encrypted = _protector.Protect(value);
            return $"{EncryptedPrefix}{encrypted}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to encrypt sensitive field value");
            throw;
        }
    }

    /// <inheritdoc />
    public string? Unprotect(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (!IsProtected(value))
        {
            return value;
        }

        try
        {
            var encryptedData = value[EncryptedPrefix.Length..];
            return _protector.Unprotect(encryptedData);
        }
        catch (CryptographicException ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt sensitive field value - returning as-is");
            return value;
        }
    }

    /// <inheritdoc />
    public bool IsProtected(string? value)
        => !string.IsNullOrEmpty(value) && value.StartsWith(EncryptedPrefix, StringComparison.Ordinal);
}
