using Microsoft.Extensions.Options;

namespace Umbraco.Automate.Deploy.Configuration;

/// <summary>
/// Provides access to the current Umbraco.Automate Deploy settings.
/// </summary>
public class UmbracoAutomateDeploySettingsAccessor(IOptionsMonitor<UmbracoAutomateDeploySettings> optionsMonitor)
{
    /// <summary>
    /// Gets the current Umbraco.Automate Deploy settings.
    /// </summary>
    public virtual UmbracoAutomateDeploySettings Settings => optionsMonitor.CurrentValue;
}
