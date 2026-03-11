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
    /// Creates a <see cref="SocketsHttpHandler"/> with SSRF protection enabled.
    /// </summary>
    public static SocketsHttpHandler Create()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = ValidatingConnectAsync,
        };

        return handler;
    }

    private static async ValueTask<Stream> ValidatingConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        // Resolve the host to IP addresses first.
        var addresses = await Dns.GetHostAddressesAsync(
            context.DnsEndPoint.Host, cancellationToken);

        if (addresses.Length == 0)
        {
            throw new SsrfException($"DNS resolution failed for host '{context.DnsEndPoint.Host}'.");
        }

        // Validate all resolved addresses before attempting connection.
        foreach (var address in addresses)
        {
            if (IsBlockedAddress(address))
            {
                throw new SsrfException(
                    $"Request to '{context.DnsEndPoint.Host}' blocked: resolved to a private or reserved address.");
            }
        }

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

    private static bool IsBlockedAddress(IPAddress address)
    {
        // Normalize IPv6-mapped IPv4 addresses (e.g. ::ffff:127.0.0.1 → 127.0.0.1).
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        // Loopback (127.0.0.0/8, ::1)
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        // Link-local (169.254.0.0/16 — includes cloud metadata 169.254.169.254, fe80::/10)
        if (address.IsIPv6LinkLocal)
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();

            // 10.0.0.0/8
            if (bytes[0] == 10)
            {
                return true;
            }

            // 172.16.0.0/12
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return true;
            }

            // 192.168.0.0/16
            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return true;
            }

            // 169.254.0.0/16 (link-local / metadata endpoint)
            if (bytes[0] == 169 && bytes[1] == 254)
            {
                return true;
            }

            // 0.0.0.0/8 (unspecified)
            if (bytes[0] == 0)
            {
                return true;
            }
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            // Unique local (fc00::/7)
            var bytes = address.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC)
            {
                return true;
            }

            // Unspecified (::)
            if (address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Any))
            {
                return true;
            }
        }

        return false;
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
