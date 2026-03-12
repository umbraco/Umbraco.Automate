# Umbraco Automate

A provider-driven automation system for Umbraco CMS. Compose triggers and actions into automations via the backoffice UI.

## Overview

Umbraco.Automate allows users to create event-driven automations directly within the Umbraco backoffice. Third-party packages extend the system by registering **providers** that expose **triggers** and **actions**.

Built on [WorkflowCore](https://github.com/danielgerlag/workflow-core).

## Key Concepts

| Term | Description |
|------|-------------|
| **Provider** | A package that contributes triggers and actions |
| **Action** | A reusable unit of work (e.g. "Send Slack Message") |
| **Trigger** | An event that starts an automation (e.g. "Content Published") |
| **Automation** | A user-defined trigger + steps sequence with draft/published lifecycle |
| **Step** | A configured instance of an action within an automation |
| **Workspace** | An admin-configured container that groups automations and scopes permissions |
| **Connection** | A named, reusable credential set for external services |
| **Run** | A single execution of an automation with per-step tracking |

## Built-in Triggers

- **Manual** - Trigger on demand
- **Scheduled** - CRON-based scheduling
- **Webhook** - HTTP endpoint with signature-based authentication
- **Content Published** - React to content publication events

## Built-in Actions

- **HTTP Request** - Call external APIs (with SSRF protection)
- **Publish Content** - Publish Umbraco content nodes
- **Log Message** - Write to the automation run log
- **Delay** - Pause execution for a configurable duration

## Installation

```bash
dotnet add package Umbraco.Automate
```

## Documentation

See the [docs](../docs/) folder for detailed documentation.

## License

MIT - See [LICENSE](../LICENSE) for details.
