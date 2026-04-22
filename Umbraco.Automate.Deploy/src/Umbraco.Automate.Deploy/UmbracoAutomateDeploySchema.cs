using Umbraco.Automate.Deploy.Configuration;

// Schema wrapper consumed by the JsonSchemaGenerate MSBuild task at build time.
// Describes the appsettings.json shape below Umbraco:Automate:Deploy so tooling
// can give editors IntelliSense against appsettings-schema.Umbraco.Automate.Deploy.json.
internal sealed class UmbracoAutomateDeploySchema
{
    /// <summary>
    /// Configuration container for all Umbraco products.
    /// </summary>
    public required UmbracoDefinition Umbraco { get; set; }

    public sealed class UmbracoDefinition
    {
        /// <summary>
        /// Configuration of Umbraco Automate.
        /// </summary>
        public required UmbracoAutomateDefinition Automate { get; set; }
    }

    public sealed class UmbracoAutomateDefinition
    {
        /// <summary>
        /// Umbraco Deploy integration for Umbraco Automate.
        /// </summary>
        public required UmbracoAutomateDeploySettings Deploy { get; set; }
    }
}
