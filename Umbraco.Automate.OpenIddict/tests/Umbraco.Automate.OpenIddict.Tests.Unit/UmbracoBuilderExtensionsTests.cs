using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using Umbraco.Automate.OpenIddict.Extensions;
using Umbraco.Cms.Core.DependencyInjection;

namespace Umbraco.Automate.OpenIddict.Tests.Unit;

public class UmbracoBuilderExtensionsTests
{
    [Theory]
    [InlineData(OpenIddictConstants.GrantTypes.AuthorizationCode)]
    [InlineData(OpenIddictConstants.GrantTypes.RefreshToken)]
    public void AddAutomateOpenIddict_EnablesTheGrantTypesOAuthConnectionsUse(string grantType)
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        var builder = new Mock<IUmbracoBuilder>();
        builder.SetupGet(b => b.Services).Returns(services);
        builder.SetupGet(b => b.Config).Returns(configuration);

        builder.Object.AddAutomateOpenIddict();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<OpenIddictClientOptions>>().Value.GrantTypes.ShouldContain(grantType);
    }
}
