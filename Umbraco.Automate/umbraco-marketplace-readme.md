## Umbraco.Automate

A provider-driven automation system for Umbraco CMS - compose triggers and actions into automations directly from the backoffice.

### Features

- **Trigger-Based Automations** - Start automations manually, on a CRON schedule, via signed webhooks, or when content is published
- **Built-in Actions** - Make HTTP requests (with SSRF protection), publish content, write log messages, and delay execution
- **Provider Extensibility** - Add-on packages contribute new triggers, actions, and connection types
- **Workspaces** - Admin-configured containers that group automations, control membership, and scope connections
- **Connections** - Named, reusable credential sets for external services, with sensitive field masking
- **Filters** - Conditional logic controlling whether steps execute
- **Run History** - Every automation execution is tracked with per-step status
- **Draft/Published Lifecycle** - Automations follow a draft/published model with version history, consistent with Umbraco content
- **Backoffice UI** - Full management interface in a dedicated Automate section

Built on [WorkflowCore](https://github.com/danielgerlag/workflow-core).

### Requirements

- Umbraco CMS 17.4.0+
- .NET 10.0
