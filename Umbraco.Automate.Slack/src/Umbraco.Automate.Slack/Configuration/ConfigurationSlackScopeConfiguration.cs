using Microsoft.Extensions.Configuration;

namespace Umbraco.Automate.Slack.Configuration;

/// <summary>
/// Reads Slack user scopes from <see cref="IConfiguration"/> at
/// <c>Umbraco:Automate:Providers:Slack:UserScopes</c>.
/// </summary>
internal sealed class ConfigurationSlackScopeConfiguration : ISlackScopeConfiguration
{
    private const string SectionPath = "Umbraco:Automate:Providers:Slack:UserScopes";

    public string[] UserScopes { get; }

    public ConfigurationSlackScopeConfiguration(IConfiguration configuration)
    {
        UserScopes = configuration.GetSection(SectionPath).Get<string[]>() ?? [];
    }
}
