using System.Globalization;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Formatting helpers shared by the built-in actions when writing entries to the step's run
/// log via <see cref="ActionContext.Log"/>, so every action describes sizes, timings and URLs
/// the same way.
/// </summary>
internal static class ActionLogFormat
{
    /// <summary>
    /// Formats a measured elapsed time, e.g. "140 ms", "2.3 s" or "1 min 5 s".
    /// </summary>
    public static string Elapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)elapsed.TotalMilliseconds} ms");
        }

        if (elapsed.TotalMinutes < 1)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{elapsed.TotalSeconds:0.0} s");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{(int)elapsed.TotalMinutes} min {elapsed.Seconds} s");
    }

    /// <summary>
    /// Formats a configured duration in words, e.g. "5 minutes" or "1 hour 30 minutes".
    /// </summary>
    public static string Duration(TimeSpan duration)
    {
        if (duration < TimeSpan.FromSeconds(1))
        {
            return string.Create(CultureInfo.InvariantCulture, $"{(int)duration.TotalMilliseconds} ms");
        }

        var parts = new List<string>(4);
        AddPart(parts, duration.Days, "day");
        AddPart(parts, duration.Hours, "hour");
        AddPart(parts, duration.Minutes, "minute");
        AddPart(parts, duration.Seconds, "second");

        return string.Join(" ", parts);
    }

    /// <summary>
    /// Formats a byte count, e.g. "512 B", "3.2 KB" or "1.4 MB".
    /// </summary>
    public static string Bytes(long bytes)
    {
        if (bytes < 1024)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes} B");
        }

        if (bytes < 1024 * 1024)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{bytes / 1024d:0.0} KB");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{bytes / (1024d * 1024d):0.0} MB");
    }

    /// <summary>
    /// Describes a URL without its user info, query string or fragment — query values often
    /// carry API keys or tokens, and the run log is visible to everyone who can see the run.
    /// A removed query string is shown as <c>?…</c> so the reader knows one was sent.
    /// </summary>
    public static string Url(Uri? uri)
    {
        if (uri is null || !uri.IsAbsoluteUri)
        {
            return "(invalid URL)";
        }

        var safe = $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
        return string.IsNullOrEmpty(uri.Query) ? safe : safe + "?…";
    }

    /// <summary>
    /// Describes a content or media item as "'Name' (key)", or just the key when the name is unknown.
    /// </summary>
    public static string Item(string? name, Guid key)
        => string.IsNullOrWhiteSpace(name) ? key.ToString() : $"'{name}' ({key})";

    /// <summary>
    /// Formats a list of cultures for a log line, e.g. "en-GB, da-DK".
    /// </summary>
    public static string Cultures(IEnumerable<string?> cultures)
        => string.Join(", ", cultures.Where(c => !string.IsNullOrWhiteSpace(c)));

    private static void AddPart(List<string> parts, int value, string unit)
    {
        if (value == 0)
        {
            return;
        }

        parts.Add(string.Create(CultureInfo.InvariantCulture, $"{value} {unit}{(value == 1 ? string.Empty : "s")}"));
    }
}
