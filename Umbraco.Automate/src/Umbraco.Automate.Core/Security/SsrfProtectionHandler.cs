using System.Net;
using System.Net.Sockets;

namespace Umbraco.Automate.Core.Security;

/// <summary>
/// A <see cref="SocketsHttpHandler"/> configured with a custom connect callback
/// that validates resolved IP addresses against SSRF denylist rules before connecting.
/// </summary>
/// <remarks>
/// This prevents Server-Side Request Forgery (SSRF) attacks by blocking outbound HTTP requests
/// to private/internal networks, loopback addresses, and cloud metadata endpoints.
/// DNS resolution happens first, then the resolved IP is validated — this prevents DNS rebinding attacks.
/// </remarks>
internal static class SsrfProtectionHandler
{
    /// <summary>
    /// Creates the primary outbound HTTP handler with SSRF protection enabled.
    /// </summary>
    /// <param name="allowProxy">
    /// Whether requests may be routed through the system/environment proxy. When a proxy is used the
    /// connect callback only sees the proxy's address, not the destination's, so the destination is
    /// instead validated by a DNS lookup before each request (and each redirect hop) is sent — see
    /// <see cref="ProxyDestinationValidationHandler"/>. When <c>false</c>, connections are always
    /// direct and validated at connect time.
    /// </param>
    /// <param name="allowAutoRedirect">Whether redirect responses are followed automatically.</param>
    public static HttpMessageHandler Create(bool allowProxy, bool allowAutoRedirect = true)
    {
        var socketsHandler = new SocketsHttpHandler
        {
            ConnectCallback = ValidatingConnectAsync,
            UseProxy = allowProxy,
            AllowAutoRedirect = allowAutoRedirect,
        };

        if (!allowProxy)
        {
            return socketsHandler;
        }

        // Redirects are followed inside SocketsHttpHandler, where an outer handler could only ever
        // see the first hop. Follow them in the validating handler instead so every hop that goes
        // through the proxy has its destination checked.
        var maxRedirects = socketsHandler.MaxAutomaticRedirections;
        socketsHandler.AllowAutoRedirect = false;

        return new ProxyDestinationValidationHandler(
            socketsHandler,
            () => socketsHandler.Proxy ?? HttpClient.DefaultProxy,
            Dns.GetHostAddressesAsync,
            allowAutoRedirect ? maxRedirects : 0);
    }

    private static async ValueTask<Stream> ValidatingConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Resolve the host to IP addresses first.
        var addresses = await Dns.GetHostAddressesAsync(
            context.DnsEndPoint.Host, cancellationToken);

        // Validate all resolved addresses before attempting connection.
        EnsureAllowed(context.DnsEndPoint.Host, addresses);

        // Connect using the first valid address.
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };

        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Throws an <see cref="SsrfException"/> when <paramref name="addresses"/> is empty or any of
    /// them is a blocked address.
    /// </summary>
    internal static void EnsureAllowed(string host, IReadOnlyCollection<IPAddress> addresses)
    {
        if (addresses.Count == 0)
        {
            throw new SsrfException($"DNS resolution failed for host '{host}'.");
        }

        if (addresses.Any(IsBlockedAddress))
        {
            throw new SsrfException(
                $"Request to '{host}' blocked: resolved to a private or reserved address.");
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the address falls in a private, loopback, link-local, reserved,
    /// multicast or otherwise non-public range that outbound requests must not reach.
    /// </summary>
    internal static bool IsBlockedAddress(IPAddress address)
    {
        // Normalize IPv6-mapped IPv4 addresses (e.g. ::ffff:127.0.0.1 → 127.0.0.1).
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsBlockedIPv4(bytes),
            AddressFamily.InterNetworkV6 => IsBlockedIPv6(address, bytes),
            _ => true,
        };
    }

    private static bool IsBlockedIPv4(ReadOnlySpan<byte> b) =>
        b[0] == 0                                          // 0.0.0.0/8 (this network)
        || b[0] == 10                                      // 10.0.0.0/8 (private)
        || (b[0] == 100 && (b[1] & 0xC0) == 64)            // 100.64.0.0/10 (shared address space)
        || b[0] == 127                                     // 127.0.0.0/8 (loopback)
        || (b[0] == 169 && b[1] == 254)                    // 169.254.0.0/16 (link-local / metadata)
        || (b[0] == 172 && (b[1] & 0xF0) == 16)            // 172.16.0.0/12 (private)
        || (b[0] == 192 && b[1] == 0 && b[2] == 0)         // 192.0.0.0/24 (IETF protocol assignments)
        || (b[0] == 192 && b[1] == 0 && b[2] == 2)         // 192.0.2.0/24 (documentation)
        || (b[0] == 192 && b[1] == 88 && b[2] == 99)       // 192.88.99.0/24 (deprecated 6to4 relay anycast)
        || (b[0] == 192 && b[1] == 168)                    // 192.168.0.0/16 (private)
        || (b[0] == 198 && (b[1] & 0xFE) == 18)            // 198.18.0.0/15 (benchmarking)
        || (b[0] == 198 && b[1] == 51 && b[2] == 100)      // 198.51.100.0/24 (documentation)
        || (b[0] == 203 && b[1] == 0 && b[2] == 113)       // 203.0.113.0/24 (documentation)
        || b[0] >= 224                                     // 224.0.0.0/4 multicast, 240.0.0.0/4 reserved, broadcast
        || (b[0] == 168 && b[1] == 63 && b[2] == 129 && b[3] == 16); // Azure platform host endpoint

    private static bool IsBlockedIPv6(IPAddress address, byte[] b)
    {
        // ::/96 — unspecified (::), loopback (::1) and deprecated IPv4-compatible addresses.
        if (IsZero(b, 0, 12))
        {
            return true;
        }

        // IPv4-translated (::ffff:0:0:0/96, SIIT) — check the embedded IPv4 address.
        if (IsZero(b, 0, 8) && b[8] == 0xFF && b[9] == 0xFF && b[10] == 0 && b[11] == 0)
        {
            return IsBlockedIPv4(b.AsSpan(12, 4));
        }

        // Link-local (fe80::/10), site-local (fec0::/10), multicast (ff00::/8).
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
        {
            return true;
        }

        // Unique local (fc00::/7).
        if ((b[0] & 0xFE) == 0xFC)
        {
            return true;
        }

        // NAT64 well-known prefix (64:ff9b::/96) — check the embedded IPv4 address.
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B && IsZero(b, 4, 8))
        {
            return IsBlockedIPv4(b.AsSpan(12, 4));
        }

        // NAT64 local-use prefix (64:ff9b:1::/48) — operator-defined translation, not public.
        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B && b[4] == 0x00 && b[5] == 0x01)
        {
            return true;
        }

        // 6to4 (2002::/16) — check the embedded IPv4 address.
        if (b[0] == 0x20 && b[1] == 0x02)
        {
            return IsBlockedIPv4(b.AsSpan(2, 4));
        }

        // Teredo (2001::/32) and documentation (2001:db8::/32).
        if (b[0] == 0x20 && b[1] == 0x01 && ((b[2] == 0x00 && b[3] == 0x00) || (b[2] == 0x0D && b[3] == 0xB8)))
        {
            return true;
        }

        // Discard-only (100::/64).
        if (b[0] == 0x01 && b[1] == 0x00 && IsZero(b, 2, 6))
        {
            return true;
        }

        return false;
    }

    private static bool IsZero(byte[] bytes, int start, int length)
    {
        for (var i = start; i < start + length; i++)
        {
            if (bytes[i] != 0)
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Exception thrown when an HTTP request is blocked by SSRF protection.
/// </summary>
public sealed class SsrfException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SsrfException"/> class.
    /// </summary>
    public SsrfException(string message) : base(message) { }
}
