# Umbraco.Automate

A provider-driven automation system for [Umbraco CMS](https://umbraco.com). Compose triggers and actions into automations via the backoffice UI.

## Packages

| Package | Description |
|---------|-------------|
| **Umbraco.Automate** | Core automation system |

## Quick Start

```bash
dotnet add package Umbraco.Automate
```

## Key Concepts

- **Providers** - Packages that contribute triggers and actions to the system
- **Actions** - Reusable units of work (e.g. "Send Slack Message", "Create Content")
- **Triggers** - Events that start an automation (e.g. "Content Published", "Form Submitted")
- **Automations** - User-defined trigger + steps sequences created in the backoffice
- **Steps** - Configured instances of actions within an automation

Built on [WorkflowCore](https://github.com/danielgerlag/workflow-core).

See [docs/vocabulary.md](docs/vocabulary.md) for the complete terminology reference.

## Development

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## License

[MIT](LICENSE)
