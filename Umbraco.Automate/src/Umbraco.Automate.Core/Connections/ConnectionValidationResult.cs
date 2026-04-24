namespace Umbraco.Automate.Core.Connections;

/// <summary>
/// Outcome of an <see cref="IConnectionType.ValidateAsync"/> check.
/// </summary>
public sealed class ConnectionValidationResult
{
    private ConnectionValidationResult(ConnectionValidationStatus status, string? message, IReadOnlyList<string>? details)
    {
        Status = status;
        Message = message;
        Details = details ?? [];
    }

    /// <summary>Gets the outcome status.</summary>
    public ConnectionValidationStatus Status { get; }

    /// <summary>Gets the primary user-facing message. May be null on Success.</summary>
    public string? Message { get; }

    /// <summary>Gets optional supporting detail lines (e.g. individual field errors).</summary>
    public IReadOnlyList<string> Details { get; }

    /// <summary>The external system responded and the credentials work.</summary>
    public static ConnectionValidationResult Success(string? message = null)
        => new(ConnectionValidationStatus.Success, message, null);

    /// <summary>
    /// The check ran but the result is inconclusive — used when the connection type has no
    /// concrete check to run, so the UI can signal "we didn't actually verify this".
    /// </summary>
    public static ConnectionValidationResult Warning(string message, IReadOnlyList<string>? details = null)
        => new(ConnectionValidationStatus.Warning, message, details);

    /// <summary>The check ran and the credentials do not work.</summary>
    public static ConnectionValidationResult Failure(string message, IReadOnlyList<string>? details = null)
        => new(ConnectionValidationStatus.Failure, message, details);
}

/// <summary>
/// Outcome status of a connection validation attempt.
/// </summary>
public enum ConnectionValidationStatus
{
    /// <summary>Credentials verified against the external system.</summary>
    Success,

    /// <summary>No concrete check was available — status is unknown.</summary>
    Warning,

    /// <summary>The external system rejected the credentials or could not be reached.</summary>
    Failure,
}
