using Microsoft.Extensions.Configuration;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Settings;

public class ConfigurationReferenceResolverTests
{
    private const string Key = "Umbraco:Automate:Variables:Bad";
    private const string BadValue = "s3cr3t-not-a-number";

    #region Sad path: a configuration value that cannot be converted to the target type

    [Theory]
    [InlineData(typeof(bool))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    public void Resolve_UnconvertibleValue_ThrowsSettingsResolutionException(Type targetType)
    {
        var exception = Record.Exception(() => Resolve(targetType));

        exception.ShouldBeOfType<SettingsResolutionException>();
    }

    [Theory]
    [InlineData(typeof(bool))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    public void Resolve_UnconvertibleValue_MessageDoesNotContainTheValue(Type targetType)
    {
        var exception = Record.Exception(() => Resolve(targetType));

        exception!.Message.ShouldNotContain(BadValue);
    }

    [Theory]
    [InlineData(typeof(bool))]
    [InlineData(typeof(int))]
    [InlineData(typeof(long))]
    [InlineData(typeof(double))]
    [InlineData(typeof(decimal))]
    public void Resolve_UnconvertibleValue_MessageContainsTheKey(Type targetType)
    {
        var exception = Record.Exception(() => Resolve(targetType));

        exception!.Message.ShouldContain(Key);
    }

    #endregion

    #region Given a convertible value

    [Fact]
    public void Resolve_ConvertibleLong_ReturnsTheLong()
    {
        var result = Resolve(typeof(long), "9000000000");

        result.ShouldBe(9000000000L);
    }

    [Fact]
    public void Resolve_ConvertibleDecimal_ReturnsTheDecimal()
    {
        var result = Resolve(typeof(decimal), "2");

        result.ShouldBe(2m);
    }

    #endregion

    private static object? Resolve(Type targetType, string value = BadValue)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Key] = value })
            .Build();

        return new ConfigurationReferenceResolver(configuration).Resolve("$" + Key, targetType, isSensitiveField: false);
    }
}
