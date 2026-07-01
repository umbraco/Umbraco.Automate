<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="./assets/logo_white.png">
    <source media="(prefers-color-scheme: light)" srcset="./assets/logo_dark.png">
    <img alt="Umbraco Automate" src="./assets/logo_dark.png" width="600">
  </picture>
  <br />
  Provider-driven automation for Umbraco CMS.
</p>

<hr />

## Products

This is a monorepo containing multiple Umbraco.Automate packages:

### Core

| Product                                   | Description                            | Version | Location            |
| ----------------------------------------- | -------------------------------------- | ------- | ------------------- |
| [**Umbraco.Automate**](Umbraco.Automate/) | Core automation system for Umbraco CMS | 0.x     | `Umbraco.Automate/` |

### Add-ons

| Product                                                                       | Description                                                 | Version | Location                       |
| ----------------------------------------------------------------------------- | ----------------------------------------------------------- | ------- | ------------------------------ |
| [**Umbraco.Automate.OpenIddict**](Umbraco.Automate.OpenIddict/)               | Reusable OAuth infrastructure via OpenIddict WebIntegration | 0.x     | `Umbraco.Automate.OpenIddict/` |
| [**Umbraco.Automate.Slack**](Umbraco.Automate.Slack/)                         | Slack connection and actions                                | 0.x     | `Umbraco.Automate.Slack/`      |

## Key Concepts

| Term           | Description                                                                        |
| -------------- | ---------------------------------------------------------------------------------- |
| **Provider**   | A package that contributes triggers and actions to the system                      |
| **Action**     | A reusable unit of work (e.g. "Send Slack Message", "Create Content")              |
| **Trigger**    | An event that starts an automation (e.g. "Content Published", "Webhook Received")  |
| **Automation** | A user-defined trigger + steps sequence created in the backoffice                  |
| **Step**       | A configured instance of an action within an automation                            |
| **Workspace**  | An admin-configured container that groups automations and scopes permissions       |
| **Connection** | A named, reusable credential set for external services                             |
| **Run**        | A single execution of an automation with per-step tracking                         |

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

## Quick Start

The fastest way to get started is using the install-demo script, which creates a unified development environment with all packages and a demo Umbraco site:

```bash
# Windows
.\scripts\install-demo-site.ps1

# Linux/Mac
./scripts/install-demo-site.sh
```

This creates:

- `Umbraco.Automate.local.slnx` - Unified solution with all products
- `demos/v18/Umbraco.Automate.DemoSite/` - Umbraco instance with all packages referenced

After running the script, build the frontend and backend:

```bash
# Install frontend dependencies
npm install

# Build all frontend packages
npm run build

# Build the unified solution
dotnet build Umbraco.Automate.local.slnx

# Run the demo site (from demos/v18/Umbraco.Automate.DemoSite/)
cd demos/v18/Umbraco.Automate.DemoSite
dotnet run
```

**Demo site credentials:** admin@example.com / password1234

## Local Development

### Prerequisites

- .NET 10.0 SDK
- Node.js 22.x
- Git

### Building Individual Products

Each product has its own solution file and can be built independently:

```bash
# Build individual products
dotnet build Umbraco.Automate/Umbraco.Automate.slnx
dotnet build Umbraco.Automate.OpenIddict/Umbraco.Automate.OpenIddict.slnx
dotnet build Umbraco.Automate.Slack/Umbraco.Automate.Slack.slnx

# Run tests
dotnet test Umbraco.Automate/Umbraco.Automate.slnx
dotnet test Umbraco.Automate.OpenIddict/Umbraco.Automate.OpenIddict.slnx
```

### Frontend Development (npm Workspaces)

This monorepo uses **npm workspaces** for frontend dependency management:

```bash
# Install all workspace dependencies (run from monorepo root)
npm install

# Build all frontends
npm run build

# Watch all frontends in parallel
npm run watch

# Regenerate OpenAPI clients (requires a running demo site)
npm run generate-client
```

## Architecture

```
Umbraco.Automate (Core)
    ├── Umbraco.Automate.OpenIddict (Add-on - depends on Core)
    └── Umbraco.Automate.Slack (Provider - depends on Core + OpenIddict)
```

Built on [WorkflowCore](https://github.com/danielgerlag/workflow-core) with a provider-driven extensibility model.

## Documentation

- [CLAUDE.md](CLAUDE.md) - Development guide, build commands, and coding standards
- Product-specific guides:
    - [Umbraco.Automate/CLAUDE.md](Umbraco.Automate/CLAUDE.md) - Core package
    - [Umbraco.Automate.OpenIddict/CLAUDE.md](Umbraco.Automate.OpenIddict/CLAUDE.md) - OAuth infrastructure add-on
    - [Umbraco.Automate.Slack/CLAUDE.md](Umbraco.Automate.Slack/CLAUDE.md) - Slack provider
- Specifications:
    - [docs/engineering-spec.md](docs/engineering-spec.md) - Full technical specification
    - [docs/functional-overview.md](docs/functional-overview.md) - Business-focused feature overview
    - [docs/identity-ownership-permissions.md](docs/identity-ownership-permissions.md) - Access control & identity spec

## Target Framework

- .NET 10.0 (`net10.0`)
- Umbraco CMS 17.x
- WorkflowCore 3.9.0
- Central Package Management via `Directory.Packages.props`

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines on:

- Development workflow and branch naming conventions
- Commit message format (conventional commits)
- Changelog generation and maintenance
- Pull request process
- Release and deployment procedures
- Coding standards

For development setup and build commands, see [CLAUDE.md](CLAUDE.md).

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.
