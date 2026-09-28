using Microsoft.AspNetCore.Authentication;
using Umbraco.Automate.OpenIddict.Controllers;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

/// <summary>
/// Open-redirect guard and nonce handling for the same-tab OAuth fallback: only same-origin,
/// root-relative URLs may be used as the post-authentication return target, always with a nonce.
/// </summary>
public class OAuthReturnUrlTests
{
    private const string Nonce = "3f2b8c1e-4d5a-4b6c-9e7f-0a1b2c3d4e5f";

    [Theory]
    [InlineData("/")]
    [InlineData("/umbraco")]
    [InlineData("/umbraco/section/automate/workspace/connection/edit/5b1f0f2e-7c1a-4f7e-9d7a-0d7a3b9d2c11")]
    [InlineData("/umbraco/section/automate/workspace/connection/create/slack?tab=settings")]
    public void IsSafe_AcceptsRootRelativePaths(string url)
    {
        OAuthReturnUrl.IsSafe(url).ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example")]
    [InlineData("http://evil.example/umbraco")]
    [InlineData("javascript:alert(1)")]
    [InlineData("//evil.example")]
    [InlineData("//evil.example/umbraco")]
    [InlineData("/\\evil.example")]
    [InlineData("\\\\evil.example")]
    [InlineData("umbraco/section")]
    [InlineData("~/umbraco")]
    [InlineData("/\t/evil.example")]
    [InlineData("/\r\n/evil.example")]
    [InlineData(" /umbraco")]
    public void IsSafe_RejectsNonLocalUrls(string? url)
    {
        OAuthReturnUrl.IsSafe(url).ShouldBeFalse();
    }

    [Fact]
    public void TryCreate_ReturnsNoTarget_ForPopupFlow()
    {
        OAuthReturnUrl.TryCreate(null, null, out var target).ShouldBeTrue();
        target.ShouldBeNull();
    }

    [Theory]
    [InlineData("/umbraco", null)]
    [InlineData(null, Nonce)]
    [InlineData("/umbraco", "")]
    [InlineData("/umbraco", "abc&credentialId=evil")]
    [InlineData("/umbraco", "abc#x")]
    [InlineData("//evil.example", Nonce)]
    public void TryCreate_Rejects_WhenEitherPartMissingOrInvalid(string? returnUrl, string? nonce)
    {
        OAuthReturnUrl.TryCreate(returnUrl, nonce, out var target).ShouldBeFalse();
        target.ShouldBeNull();
    }

    [Fact]
    public void TryCreate_Rejects_OverlongNonce()
    {
        OAuthReturnUrl.TryCreate("/umbraco", new string('a', 129), out _).ShouldBeFalse();
    }

    [Fact]
    public void WriteTo_ThenReadFrom_RoundTrips()
    {
        var properties = new AuthenticationProperties();
        new OAuthReturnUrl("/umbraco/section/automate", Nonce).WriteTo(properties);

        OAuthReturnUrl.ReadFrom(properties).ShouldBe(new OAuthReturnUrl("/umbraco/section/automate", Nonce));
    }

    [Fact]
    public void ReadFrom_ReturnsNull_WhenStoredUrlIsNotLocal()
    {
        var properties = new AuthenticationProperties();
        properties.Items[OAuthReturnUrl.UrlPropertyKey] = "//evil.example";
        properties.Items[OAuthReturnUrl.NoncePropertyKey] = Nonce;

        OAuthReturnUrl.ReadFrom(properties).ShouldBeNull();
    }

    [Fact]
    public void Success_PutsNonceAndCredentialIdInFragment_NotQueryString()
    {
        var result = new OAuthReturnUrl("/umbraco/section/automate?x=1", Nonce).Success("Slack", "abc-123");

        result.Url.ShouldBe($"/umbraco/section/automate?x=1#automate-oauth=1&provider=Slack&nonce={Nonce}&credentialId=abc-123");
    }

    [Fact]
    public void Failure_EncodesErrorInFragment_AndReplacesAnyExistingFragment()
    {
        var result = new OAuthReturnUrl("/umbraco#old", Nonce).Failure("Slack", "Bad & <worse>");

        result.Url.ShouldBe($"/umbraco#automate-oauth=1&provider=Slack&nonce={Nonce}&error=Bad%20%26%20%3Cworse%3E");
    }
}
