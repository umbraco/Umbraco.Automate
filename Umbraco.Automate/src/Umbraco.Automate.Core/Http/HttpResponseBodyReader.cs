using System.Text;

namespace Umbraco.Automate.Core.Http;

/// <summary>
/// Reads HTTP response bodies as text while enforcing a maximum size, so a single oversized
/// payload can never be buffered whole into memory. Shared by the HTTP Request action and the
/// Run Script action's <c>fetch</c> implementation so both enforce the same cap.
/// </summary>
public static class HttpResponseBodyReader
{
    /// <summary>
    /// Reads <paramref name="content"/> up to <paramref name="maxBytes"/>, returning <c>null</c>
    /// when the body turns out to be larger. The cap is enforced while streaming, so at most one
    /// chunk past the limit is ever buffered.
    /// </summary>
    /// <param name="content">The response content to read.</param>
    /// <param name="maxBytes">The maximum number of bytes to buffer.</param>
    /// <param name="cancellationToken">A token to cancel the read.</param>
    /// <returns>The decoded body, or <c>null</c> if it exceeds <paramref name="maxBytes"/>.</returns>
    public static async Task<string?> ReadCappedAsync(HttpContent content, long maxBytes, CancellationToken cancellationToken)
    {
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return ResolveEncoding(content.Headers.ContentType?.CharSet).GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static Encoding ResolveEncoding(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset))
        {
            return Encoding.UTF8;
        }

        try
        {
            return Encoding.GetEncoding(charset.Trim('"'));
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }
}
