using System.Text;

namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// Shared detection and scanning of <c>$Section:Path</c> configuration references embedded in
/// settings string values. Used by both <see cref="EditableModelResolver"/> (to substitute
/// references) and <see cref="EditableModelSerializer"/> (to decide whether a value is a
/// configuration pointer and must not be encrypted), so the two cannot drift apart.
/// </summary>
/// <remarks>
/// A reference is a <c>$</c> immediately followed by one or more key characters
/// (<c>A-Z a-z 0-9 _ . : -</c>) whose key falls under one of the caller's allowed prefixes.
/// <list type="bullet">
/// <item><description><c>$$</c> is an escape that collapses to a literal <c>$</c>.</description></item>
/// <item><description><c>${ ... }</c> bindings are never captured — <c>{</c> is not a key character,
/// so the <c>$</c> is left literal and the binding subsystem handles it later.</description></item>
/// <item><description>A <c>$token</c> whose key does not match an allowed prefix is left literal
/// (e.g. a password like <c>p$ssw0rd</c>) rather than treated as a reference.</description></item>
/// </list>
/// </remarks>
internal static class ConfigurationReferenceScanner
{
    private const char Sigil = '$';

    /// <summary>
    /// Determines whether <paramref name="c"/> is a valid configuration-key character.
    /// </summary>
    private static bool IsKeyChar(char c) =>
        char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or ':' or '-';

    /// <summary>
    /// Determines whether the entire value is a single reference token (<c>"$" + key</c>) with
    /// nothing else around it. The whole-value case keeps its own gate chain and type conversion,
    /// so it is handled separately from embedded scanning.
    /// </summary>
    public static bool IsWholeReference(string value, out string key)
    {
        key = string.Empty;

        if (value.Length < 2 || value[0] != Sigil)
        {
            return false;
        }

        for (var i = 1; i < value.Length; i++)
        {
            if (!IsKeyChar(value[i]))
            {
                return false;
            }
        }

        key = value[1..];
        return true;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="value"/> contains at least one reference token whose
    /// key matches one of <paramref name="allowedPrefixes"/>.
    /// </summary>
    public static bool ContainsReference(string? value, IReadOnlyList<string> allowedPrefixes)
    {
        if (string.IsNullOrEmpty(value) || !value.Contains(Sigil))
        {
            return false;
        }

        var found = false;
        Scan(value, allowedPrefixes, _ =>
        {
            found = true;
            return null; // Detection only — leave the token in place.
        });

        return found;
    }

    /// <summary>
    /// Walks <paramref name="value"/> and rebuilds it, invoking <paramref name="substitute"/> for
    /// each reference token whose key matches an allowed prefix. When the callback returns a
    /// non-<c>null</c> string it is spliced in place of the token; when it returns <c>null</c> the
    /// token is left literal. Tokens outside the allow-list, <c>$$</c> escapes and <c>${ }</c>
    /// bindings are handled per the type remarks.
    /// </summary>
    public static string Scan(string value, IReadOnlyList<string> allowedPrefixes, Func<string, string?> substitute)
    {
        var builder = new StringBuilder(value.Length);
        var i = 0;

        while (i < value.Length)
        {
            var c = value[i];
            if (c != Sigil)
            {
                builder.Append(c);
                i++;
                continue;
            }

            // "$$" → literal "$".
            if (i + 1 < value.Length && value[i + 1] == Sigil)
            {
                builder.Append(Sigil);
                i += 2;
                continue;
            }

            // Read the key characters following the "$".
            var start = i + 1;
            var end = start;
            while (end < value.Length && IsKeyChar(value[end]))
            {
                end++;
            }

            // No key (e.g. a trailing "$" or a "${" binding) → leave the "$" literal.
            if (end == start)
            {
                builder.Append(Sigil);
                i++;
                continue;
            }

            var key = value[start..end];
            if (MatchesPrefix(key, allowedPrefixes))
            {
                var replacement = substitute(key);
                if (replacement is null)
                {
                    builder.Append(Sigil).Append(key);
                }
                else
                {
                    builder.Append(replacement);
                }
            }
            else
            {
                // Not an allowed reference (e.g. a stray "$" in a password) → leave it literal.
                builder.Append(Sigil).Append(key);
            }

            i = end;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Determines whether <paramref name="configKey"/> falls under one of <paramref name="prefixes"/>.
    /// Matching is segment-aware (a prefix matches the whole key or a key whose next character
    /// is the <c>:</c> section separator) and case-insensitive, so <c>Umbraco:Automate:Secrets</c>
    /// permits <c>Umbraco:Automate:Secrets:Token</c> but not <c>Umbraco:Automate:SecretsBackup:Token</c>.
    /// </summary>
    public static bool MatchesPrefix(string configKey, IReadOnlyList<string> prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (!configKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (configKey.Length == prefix.Length || configKey[prefix.Length] == ':')
            {
                return true;
            }
        }

        return false;
    }
}
