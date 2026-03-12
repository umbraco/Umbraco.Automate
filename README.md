# Umbraco.Automate

A provider-driven automation system for [Umbraco CMS](https://umbraco.com). Compose triggers and actions into automations via the backoffice UI.

## Packages

| Package | Description |
|---------|-------------|
| **Umbraco.Automate** | Core automation system |
| **Umbraco.Automate.OpenIddict** | Reusable OAuth infrastructure via OpenIddict WebIntegration |
| **Umbraco.Automate.Slack** | Slack connection and actions |

## Quick Start

```bash
dotnet add package Umbraco.Automate
```

## Key Concepts

| Term | Description |
|------|-------------|
| **Provider** | A package that contributes triggers and actions to the system |
| **Action** | A reusable unit of work (e.g. "Send Slack Message", "Create Content") |
| **Trigger** | An event that starts an automation (e.g. "Content Published", "Webhook Received") |
| **Automation** | A user-defined trigger + steps sequence created in the backoffice |
| **Step** | A configured instance of an action within an automation |
| **Workspace** | An admin-configured container that groups automations and scopes permissions |
| **Connection** | A named, reusable credential set for external services |
| **Run** | A single execution of an automation with per-step tracking |

Built on [WorkflowCore](https://github.com/danielgerlag/workflow-core).

See [docs/vocabulary.md](docs/vocabulary.md) for the complete terminology reference.

## Features

### Built-in Triggers

- **Manual** - Trigger an automation on demand
- **Scheduled** - CRON-based scheduling
- **Webhook** - HTTP endpoint with signature-based authentication
- **Content Published** - React to Umbraco content publication events

### Built-in Actions

- **HTTP Request** - Call external APIs (with SSRF protection)
- **Publish Content** - Publish Umbraco content nodes
- **Log Message** - Write to the automation run log
- **Delay** - Pause execution for a configurable duration

### Workspaces & Permissions

Automations are organised into **workspaces** - admin-configured containers that control:

- Which users have access (membership)
- Which connections are available
- The service account used for execution

### Connections

Named, reusable credential sets for external services. Connections are scoped to workspaces and support an extensible type system for custom connection types. The **Umbraco.Automate.OpenIddict** package adds OAuth connection support via OpenIddict Client WebIntegration, with 100+ pre-configured providers.

### Versioning & Publishing

Automations follow a draft/published lifecycle consistent with the Umbraco content model, with full version history tracking.

## Development

### Prerequisites

- .NET 10.0 SDK
- Node.js 20.x
- Git

### Build & Test

```bash
# Core
dotnet build Umbraco.Automate/Umbraco.Automate.slnx
dotnet test Umbraco.Automate/Umbraco.Automate.slnx

# OpenIddict (OAuth infrastructure)
dotnet build Umbraco.Automate.OpenIddict/Umbraco.Automate.OpenIddict.slnx
dotnet test Umbraco.Automate.OpenIddict/Umbraco.Automate.OpenIddict.slnx

# Slack provider
dotnet build Umbraco.Automate.Slack/Umbraco.Automate.Slack.slnx
```

### Demo Site

A demo Umbraco site is available at `demo/Umbraco.Automate.DemoSite/` for manual testing.

### Project Structure

```
Umbraco.Automate/
├── src/
│   ├── Umbraco.Automate/                    # Meta-package (NuGet distribution)
│   ├── Umbraco.Automate.Core/              # Domain models, services, interfaces
│   ├── Umbraco.Automate.Web/               # Management API
│   ├── Umbraco.Automate.Web.StaticAssets/   # TypeScript/Lit frontend
│   ├── Umbraco.Automate.Persistence/        # EF Core DbContext, repositories
│   ├── Umbraco.Automate.Persistence.SqlServer/
│   ├── Umbraco.Automate.Persistence.Sqlite/
│   └── Umbraco.Automate.Startup/            # Umbraco Composer, DI registration
├── tests/
│   ├── Umbraco.Automate.Tests.Unit/
│   ├── Umbraco.Automate.Tests.Integration/
│   └── Umbraco.Automate.Tests.Common/
└── docs/
    ├── engineering-spec.md                   # Full technical specification
    ├── functional-overview.md                # Business-focused feature overview
    ├── identity-ownership-permissions.md     # Access control & identity spec
    └── vocabulary.md                         # Standard terminology
```

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup and guidelines.

## License

[MIT](LICENSE)
