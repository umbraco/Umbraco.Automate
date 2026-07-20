namespace Umbraco.Automate.Core;

/// <summary>
/// Thrown when a database write is rejected because Automate's startup migrations failed,
/// so <see cref="AutomateReadinessSignal"/> will never signal success.
/// </summary>
public sealed class AutomateNotReadyException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutomateNotReadyException"/> class.
    /// </summary>
    public AutomateNotReadyException(Exception migrationFailure)
        : base("Automate startup migrations failed, so the database is not ready for use. See the inner exception for the migration failure.", migrationFailure)
    {
    }
}
