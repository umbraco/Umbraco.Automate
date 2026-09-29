using Microsoft.AspNetCore.DataProtection;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

public class OAuthCredentialsHandoffProtectorTests
{
    private readonly EphemeralDataProtectionProvider _dataProtectionProvider = new();
    private readonly AdjustableTimeProvider _time = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
    private readonly OAuthCredentialsHandoffProtector _protector;

    public OAuthCredentialsHandoffProtectorTests()
    {
        _protector = new OAuthCredentialsHandoffProtector(_dataProtectionProvider, _time);
    }

    [Fact]
    public void Unprotect_RoundTripsCredentialIdAndProvider()
    {
        var id = Guid.NewGuid();

        var handoff = _protector.Unprotect(_protector.Protect(id, "Slack"));

        handoff.Status.ShouldBe(OAuthCredentialsHandoffStatus.Valid);
        handoff.CredentialId.ShouldBe(id);
        handoff.Provider.ShouldBe("Slack");
    }

    [Fact]
    public void Protect_DoesNotExposeCredentialId()
    {
        var id = Guid.NewGuid();

        var token = _protector.Protect(id, "Slack");

        token.ShouldNotContain(id.ToString());
        token.ShouldNotContain(id.ToString("N"));
    }

    [Fact]
    public void Unprotect_ValidJustBeforeLifetimeEnds()
    {
        var token = _protector.Protect(Guid.NewGuid(), "Slack");

        _time.Advance(OAuthCredentialsHandoffProtector.Lifetime - TimeSpan.FromSeconds(1));

        _protector.Unprotect(token).Status.ShouldBe(OAuthCredentialsHandoffStatus.Valid);
    }

    [Fact]
    public void Unprotect_Expired_AfterLifetime_ButStillIdentifiesCredential()
    {
        var id = Guid.NewGuid();
        var token = _protector.Protect(id, "Slack");

        _time.Advance(OAuthCredentialsHandoffProtector.Lifetime);

        var handoff = _protector.Unprotect(token);
        handoff.Status.ShouldBe(OAuthCredentialsHandoffStatus.Expired);
        handoff.CredentialId.ShouldBe(id);
    }

    [Fact]
    public void Unprotect_Invalid_WhenTampered()
    {
        var token = _protector.Protect(Guid.NewGuid(), "Slack");
        var tampered = token[..^2] + (token[^2] == 'A' ? 'B' : 'A') + token[^1];

        _protector.Unprotect(tampered).Status.ShouldBe(OAuthCredentialsHandoffStatus.Invalid);
    }

    [Fact]
    public void Unprotect_Invalid_WhenProtectedForAnotherPurpose()
    {
        var foreign = _dataProtectionProvider.CreateProtector("Some.Other.Purpose")
            .Protect($$"""{"Id":"{{Guid.NewGuid()}}","Provider":"Slack","Exp":9999999999}""");

        _protector.Unprotect(foreign).Status.ShouldBe(OAuthCredentialsHandoffStatus.Invalid);
    }

    [Fact]
    public void Unprotect_Invalid_WhenIssuedByAnotherKeyRing()
    {
        var other = new OAuthCredentialsHandoffProtector(new EphemeralDataProtectionProvider(), _time);

        _protector.Unprotect(other.Protect(Guid.NewGuid(), "Slack")).Status.ShouldBe(OAuthCredentialsHandoffStatus.Invalid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-token")]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301")]
    public void Unprotect_Invalid_ForNonTokens(string? value)
    {
        _protector.Unprotect(value).Status.ShouldBe(OAuthCredentialsHandoffStatus.Invalid);
    }
}

/// <summary>
/// A <see cref="TimeProvider"/> whose clock only moves when told to.
/// </summary>
internal sealed class AdjustableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now = _now.Add(by);
}
