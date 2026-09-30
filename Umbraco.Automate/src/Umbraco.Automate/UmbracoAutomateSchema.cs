using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Messaging;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Versioning;

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
        /// Run Script action sandbox configuration.
        /// </summary>
        public required ScriptingOptions Scripting { get; set; }

        /// <summary>
        /// Outbox message dispatcher configuration.
        /// </summary>
        public required OutboxOptions Outbox { get; set; }

        /// <summary>
        /// Version history cleanup configuration.
        /// </summary>
        public required VersionCleanupPolicy VersionCleanup { get; set; }

        /// <summary>
        /// Run history cleanup configuration.
        /// </summary>
        public required RunCleanupPolicy RunCleanup { get; set; }

        /// <summary>
        /// Scheduled trigger background job configuration.
        /// </summary>
        public required ScheduledTriggerOptions ScheduledTrigger { get; set; }

        /// <summary>
        /// Per-automation rate limiting configuration.
        /// </summary>
        public required RateLimitingOptions RateLimiting { get; set; }

        /// <summary>
        /// Circuit breaker (auto-disable) configuration.
        /// </summary>
        public required CircuitBreakerOptions CircuitBreaker { get; set; }

        /// <summary>
        /// WorkflowCore distributed lock configuration.
        /// </summary>
        public required WorkflowLockOptions WorkflowLock { get; set; }
    }
}
