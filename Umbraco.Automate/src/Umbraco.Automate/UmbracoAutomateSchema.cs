using Umbraco.Automate.Core.Configuration;

// Schema wrapper consumed by the JsonSchemaGenerate MSBuild task at build time.
// Describes the appsettings.json shape below Umbraco:Automate so tooling can give
// editors IntelliSense against appsettings-schema.Umbraco.Automate.json.
internal sealed class UmbracoAutomateSchema
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

    /// <summary>
    /// Root Umbraco.Automate configuration (<c>Umbraco:Automate</c>). Inherits the
    /// root-level switches from <see cref="AutomateOptions"/> and adds the
    /// known sub-section options as nested objects.
    /// </summary>
    public sealed class UmbracoAutomateDefinition : AutomateOptions
    {
        /// <summary>
        /// Webhook endpoint configuration.
        /// </summary>
        public required WebhookOptions Webhook { get; set; }

        /// <summary>
        /// Automation execution configuration.
        /// </summary>
        public required ExecutionOptions Execution { get; set; }

        /// <summary>
        /// Governance and audit configuration.
        /// </summary>
        public required GovernanceOptions Governance { get; set; }

        /// <summary>
        /// Scheduled trigger background job configuration.
        /// </summary>
        public required ScheduledTriggerOptions ScheduledTrigger { get; set; }

        /// <summary>
        /// Circuit breaker (auto-disable) configuration.
        /// </summary>
        public required CircuitBreakerOptions CircuitBreaker { get; set; }
    }
}
