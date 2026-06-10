using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Security;

public class SensitiveFieldProtectorTests
{
    private readonly SensitiveFieldProtector _protector;

    public SensitiveFieldProtectorTests()
    {
        var dataProtectionProvider = DataProtectionProvider.Create("Umbraco.Automate.Tests");
        _protector = new SensitiveFieldProtector(
            dataProtectionProvider,
            Mock.Of<ILogger<SensitiveFieldProtector>>());
    }

    #region Protect

    [Fact]
    public void Protect_WithNullValue_ReturnsNull()
    {
        var result = _protector.Protect(null);

        result.ShouldBeNull();
    }

    [Fact]
    public void Protect_WithEmptyValue_ReturnsEmpty()
    {
        var result = _protector.Protect(string.Empty);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Protect_WithPlainValue_ReturnsEncryptedWithPrefix()
    {
        var result = _protector.Protect("my-secret");

        result.ShouldNotBeNull();
        result.ShouldStartWith("ENC:");
        result.ShouldNotContain("my-secret");
    }

    [Fact]
    public void Protect_WithAlreadyProtectedValue_ReturnsAsIs()
    {
        var firstProtected = _protector.Protect("my-secret");
        var secondProtected = _protector.Protect(firstProtected);

        secondProtected.ShouldBe(firstProtected);
    }

    #endregion

    #region Unprotect

    [Fact]
    public void Unprotect_WithNullValue_ReturnsNull()
    {
        var result = _protector.Unprotect(null);

        result.ShouldBeNull();
    }

    [Fact]
    public void Unprotect_WithEmptyValue_ReturnsEmpty()
    {
        var result = _protector.Unprotect(string.Empty);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Unprotect_WithPlainValue_ReturnsAsIs()
    {
        var result = _protector.Unprotect("not-encrypted");

        result.ShouldBe("not-encrypted");
    }

    [Fact]
    public void Unprotect_WithProtectedValue_ReturnsDecrypted()
    {
        var encrypted = _protector.Protect("my-secret");
        var decrypted = _protector.Unprotect(encrypted);

        decrypted.ShouldBe("my-secret");
    }

    [Fact]
    public void Unprotect_WithCorruptedData_ReturnsAsIs()
    {
        var result = _protector.Unprotect("ENC:corrupted-garbage-data");

        result.ShouldBe("ENC:corrupted-garbage-data");
    }

    #endregion

    #region IsProtected

    [Fact]
    public void IsProtected_WithNull_ReturnsFalse()
    {
        _protector.IsProtected(null).ShouldBeFalse();
    }

    [Fact]
    public void IsProtected_WithEmpty_ReturnsFalse()
    {
        _protector.IsProtected(string.Empty).ShouldBeFalse();
    }

    [Fact]
    public void IsProtected_WithPlainValue_ReturnsFalse()
    {
        _protector.IsProtected("plain-value").ShouldBeFalse();
    }

    [Fact]
    public void IsProtected_WithEncPrefix_ReturnsTrue()
    {
        _protector.IsProtected("ENC:something").ShouldBeTrue();
    }

    #endregion

    #region Round Trip

    [Theory]
    [InlineData("simple-secret")]
    [InlineData("a")]
    [InlineData("super-long-secret-key-with-special-chars-!@#$%^&*()")]
    [InlineData("unicode: こんにちは")]
    public void ProtectAndUnprotect_RoundTrip_PreservesValue(string original)
    {
        var encrypted = _protector.Protect(original);
        var decrypted = _protector.Unprotect(encrypted);

        decrypted.ShouldBe(original);
    }

    #endregion
}
