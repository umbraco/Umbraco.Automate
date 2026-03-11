namespace Umbraco.Automate.Core.Persistence;

/// <summary>
/// Helper methods for classifying database exceptions.
/// </summary>
internal static class DatabaseExceptions
{
    /// <summary>
    /// Returns <c>true</c> if the exception (or any inner exception) indicates
    /// a missing database table — e.g. before Automate migrations have completed.
    /// </summary>
    internal static bool IsMissingTable(Exception ex)
    {
        // Walk the exception chain looking for provider-specific messages:
        //   SQLite:     "no such table: ..."
        //   SQL Server: "Invalid object name '...'"
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("no such table", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Invalid object name", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
