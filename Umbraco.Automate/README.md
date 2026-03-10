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
| **Automation** | A user-defined trigger + steps sequence |
| **Step** | A configured instance of an action within an automation |

## Installation

```bash
dotnet add package Umbraco.Automate
```

## Documentation

See the [docs](../docs/) folder for detailed documentation.

## License

MIT - See [LICENSE](../LICENSE) for details.
