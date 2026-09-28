using System.Net;
using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Security;

public class SsrfProtectionHandlerTests
{
    [Theory]
    // Previously covered ranges (regression)
    [InlineData("127.0.0.1")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.0")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd00::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:10.0.0.1")]
    // Shared address space 100.64.0.0/10
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.254")]
    // Benchmarking 198.18.0.0/15
    [InlineData("198.18.0.1")]
    [InlineData("198.19.255.254")]
    // IETF protocol assignments / documentation
    [InlineData("192.0.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    // Multicast, reserved, broadcast
    [InlineData("224.0.0.1")]
    [InlineData("239.255.255.250")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    // Azure platform host endpoint
    [InlineData("168.63.129.16")]
    [InlineData("::ffff:168.63.129.16")]
    // IPv6 site-local fec0::/10
    [InlineData("fec0::1")]
    [InlineData("feff::1")]
    // IPv6 multicast ff00::/8
    [InlineData("ff02::1")]
    [InlineData("ff05::2")]
    // Deprecated IPv4-compatible ::/96
    [InlineData("::127.0.0.1")]
    [InlineData("::8.8.8.8")]
    // NAT64 well-known prefix with non-public embedded IPv4
    [InlineData("64:ff9b::7f00:1")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("64:ff9b::a00:1")]
    // NAT64 local-use prefix
    [InlineData("64:ff9b:1::1")]
    [InlineData("64:ff9b:1:ffff::808:808")]
    // 6to4 with non-public embedded IPv4
    [InlineData("2002:7f00:1::1")]
    [InlineData("2002:a9fe:a9fe::1")]
    [InlineData("2002:c0a8:101::1")]
    // Teredo
    [InlineData("2001::1")]
    [InlineData("2001:0:4136:e378:8000:63bf:3fff:fdd2")]
    // IPv6 documentation / discard-only
    [InlineData("2001:db8::1")]
    [InlineData("100::1")]
    // Deprecated 6to4 relay anycast 192.88.99.0/24
    [InlineData("192.88.99.1")]
    [InlineData("192.88.99.255")]
    [InlineData("::ffff:192.88.99.1")]
    // IPv4-translated ::ffff:0:0:0/96 with non-public embedded IPv4
    [InlineData("::ffff:0:7f00:1")]
    [InlineData("::ffff:0:a9fe:a9fe")]
    [InlineData("::ffff:0:c0a8:101")]
    public void IsBlockedAddress_NonPublicAddress_ReturnsTrue(string ip)
    {
        SsrfProtectionHandler.IsBlockedAddress(IPAddress.Parse(ip)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("93.184.216.34")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.1")]
    [InlineData("172.15.255.255")]
    [InlineData("172.32.0.1")]
    [InlineData("198.17.255.255")]
    [InlineData("198.20.0.1")]
    [InlineData("168.63.129.17")]
    [InlineData("223.255.255.254")]
    [InlineData("::ffff:8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("2001:4860:4860::8888")]
    [InlineData("64:ff9b::808:808")]
    [InlineData("2002:808:808::1")]
    [InlineData("192.88.98.255")]
    [InlineData("192.88.100.1")]
    [InlineData("::ffff:0:808:808")]
    public void IsBlockedAddress_PublicAddress_ReturnsFalse(string ip)
    {
        SsrfProtectionHandler.IsBlockedAddress(IPAddress.Parse(ip)).ShouldBeFalse();
    }

    [Fact]
    public void Create_WithoutProxy_ReturnsDirectHandler()
    {
        using var handler = SsrfProtectionHandler.Create(allowProxy: false);

        var sockets = handler.ShouldBeOfType<SocketsHttpHandler>();
        sockets.UseProxy.ShouldBeFalse();
        sockets.AllowAutoRedirect.ShouldBeTrue();
        sockets.ConnectCallback.ShouldNotBeNull();
    }

    [Fact]
    public void Create_WithoutProxyAndNoRedirect_DisablesRedirects()
    {
        using var handler = SsrfProtectionHandler.Create(allowProxy: false, allowAutoRedirect: false);

        handler.ShouldBeOfType<SocketsHttpHandler>().AllowAutoRedirect.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Create_WithProxy_WrapsHandlerThatDoesNotFollowRedirectsItself(bool allowAutoRedirect)
    {
        using var handler = SsrfProtectionHandler.Create(allowProxy: true, allowAutoRedirect);

        var validating = handler.ShouldBeOfType<ProxyDestinationValidationHandler>();
        var sockets = validating.InnerHandler.ShouldBeOfType<SocketsHttpHandler>();
        sockets.UseProxy.ShouldBeTrue();
        sockets.AllowAutoRedirect.ShouldBeFalse();
        sockets.ConnectCallback.ShouldNotBeNull();
    }

    [Fact]
    public void ExecutionOptions_AllowOutboundHttpProxy_DefaultsToTrue()
    {
        new Umbraco.Automate.Core.Configuration.ExecutionOptions().AllowOutboundHttpProxy.ShouldBeTrue();
    }
}
