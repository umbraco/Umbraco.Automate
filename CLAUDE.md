# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Structure

This is a monorepo containing Umbraco.Automate and its add-on packages:

**Core:**

| Product                  | Description                                        | Location             |
| ------------------------ | -------------------------------------------------- | -------------------- |
| **Umbraco.Automate**     | Core automation system for Umbraco CMS             | `Umbraco.Automate/`  |

**Add-on packages:**

| Product                          | Description                                            | Location                        |
| -------------------------------- | ------------------------------------------------------ | ------------------------------- |
| **Umbraco.Automate.OpenIddict**  | Reusable OAuth infrastructure via OpenIddict WebIntegration | `Umbraco.Automate.OpenIddict/` |
| **Umbraco.Automate.Slack**       | Slack connection and actions                           | `Umbraco.Automate.Slack/`      |

Each product has its own solution file, CLAUDE.md, and can be built independently. For detailed guidance on a specific product, see its CLAUDE.md file.

## Development Environment

### Prerequisites

- .NET 10.0 SDK
- Node.js 22.x
- Git

### Demo Site

A demo Umbraco site is available at `demo/Umbraco.Automate.DemoSite/` for manual testing. Use the `/demo-site-management` skill to start/stop it.

## Build Commands

### .NET

```bash
# Build individual product
dotnet build Umbraco.Automate/Umbraco.Automate.slnx

# Run tests for a product
dotnet test Umbraco.Automate/Umbraco.Automate.slnx
```

## Architecture Overview

### Standard Project Structure

**Core package** (Umbraco.Automate) follows this structure:

```
ProductName/
├── src/
│   ├── ProductName.Core/           # Domain models, services, interfaces
│   ├── ProductName.Web/            # Management API (controllers, models)
│   ├── ProductName.Web.StaticAssets/  # TypeScript/Lit frontend
│   │   └── Client/                 # npm project
│   ├── ProductName.Persistence/    # EF Core DbContext, repositories
│   ├── ProductName.Persistence.SqlServer/  # SQL Server migrations
│   ├── ProductName.Persistence.Sqlite/     # SQLite migrations
│   ├── ProductName.Startup/        # Umbraco Composer for DI
│   └── ProductName/                # Meta-package bundling all
├── tests/
│   ├── ProductName.Tests.Unit/
│   ├── ProductName.Tests.Integration/
│   └── ProductName.Tests.Common/
├── ProductName.slnx                # Individual solution (XML solution format)
└── CLAUDE.md                       # Product-specific guidance
```

### Core Concepts (Umbraco.Automate)

- **Providers** - Packages that contribute triggers and actions
- **Actions** - Reusable units of work (e.g. "Send Slack Message")
- **Triggers** - Events that start an automation (e.g. "Content Published")
- **Settings** - POCO models on actions/triggers that drive the config UI via EditableModels infrastructure
- **Automations** - User-defined trigger + steps sequences with draft/published lifecycle
- **Steps** - Configured instances of actions within an automation
- **Inputs/Outputs** - Runtime data flowing between steps
- **Filters** - Conditional logic controlling step execution
- **Runs** - Single executions of an automation, tracked with per-step status
- **Workspaces** - Admin-configured containers that group automations, control membership, and scope connections
- **Connections** - Named, reusable credential sets for external services, scoped to workspaces
- **Service Accounts** - Execution identity for automations (UserKind.Api), tied to workspaces

Built on [WorkflowCore](https://github.com/danielgerlag/workflow-core) with a provider-driven extensibility model.

See [docs/vocabulary.md](docs/vocabulary.md) for the complete terminology reference.

### WorkflowCore First

**Always prefer existing WorkflowCore features over building custom equivalents.** Before implementing anything related to workflow execution, persistence, scheduling, events, retries, compensation, branching, or step orchestration, check whether WorkflowCore already provides it (see the [WorkflowCore docs](https://github.com/danielgerlag/workflow-core) and source).

- Reuse built-in step bodies, control-flow primitives (`If`, `While`, `ForEach`, `Parallel`, `Schedule`, `Saga`, etc.), event handling, and persistence interfaces wherever possible.
- Extend via the documented extension points (custom `IStepBody`, `IWorkflowMiddleware`, `IPersistenceProvider`, `ILifeCycleEventHub`, etc.) rather than wrapping or replacing the engine.
- Only build a custom implementation when there is a concrete, documented reason WorkflowCore cannot meet the requirement (e.g. EF Core version conflict in the existing `IPersistenceProvider`, or a feature genuinely missing from the engine). Capture the reason in a comment, commit message, or design doc so future maintainers know why the deviation exists.
- When in doubt, ask before reimplementing — a custom implementation is the exception, not the default.

### Key Domain Services

| Service | Responsibility |
| --- | --- |
| `IAutomationService` | CRUD + publish/unpublish lifecycle |
| `IWorkspaceService` | Workspace management and membership |
| `IConnectionService` | Connection CRUD |
| `IAutomationRunService` | Run tracking and history |
| `IEntityVersionService` | Version history |

### Runtime & Dispatch

- Custom lightweight outbox pattern (`IOutboxStore`, `OutboxDispatcher`) for reliable trigger dispatch — replaced DotNetCore.CAP
- `TriggerEventHandler` / `TriggerNotificationHandler` integrate with Umbraco notifications
- Custom `IPersistenceProvider` for WorkflowCore (avoids external EF provider to prevent EF Core version conflicts)

### Security

- Workspace-based access control with membership checks
- Automate section access authorization policy
- Service account execution identity (runs as `UserKind.Api`)
- SSRF protection handler for HTTP actions
- Sensitive field masking for credentials in API responses

### Built-in Triggers

| Trigger | Description |
| --- | --- |
| `ManualTrigger` | Manual invocation |
| `ScheduledTrigger` | CRON-based scheduling |
| `WebhookTrigger` | HTTP webhook endpoint with signature auth |
| `ContentPublishedTrigger` | Content publication events |

### Built-in Actions

| Action | Description |
| --- | --- |
| `DelayAction` | Pause execution for a duration |
| `HttpRequestAction` | Make HTTP requests (with SSRF protection) |
| `LogMessageAction` | Write to automation run log |
| `PublishContentAction` | Publish Umbraco content |

### Versioning & Publishing

Automations follow a draft/published lifecycle consistent with the Umbraco content model. Version history is tracked via `IEntityVersionService`.

## Key Files

| File                                    | Purpose                                                |
| --------------------------------------- | ------------------------------------------------------ |
| `<Product>/version.json`                | Per-product version (Nerdbank.GitVersioning)           |
| `<Product>/changelog.config.json`       | Per-product scopes for changelog generation            |
| `<Product>/CHANGELOG.md`               | Per-product changelog (auto-generated from git history)|
| `docs/engineering-spec.md`              | Full technical specification                           |
| `docs/functional-overview.md`           | Business-focused feature overview                      |
| `docs/identity-ownership-permissions.md`| Workspaces, service accounts, access control spec      |
| `docs/vocabulary.md`                    | Standard terminology reference                         |

## Database

- SQL Server and SQLite supported via EF Core
- Each product has its own migrations:
  - Core prefix: `UmbracoAutomate_`
  - OpenIddict prefix: `UmbracoAutomateOpenIddict_`
- Core domain tables: Automation, Step, AutomationRun, StepRun, Workspace, Connection, OutboxMessage
- Core engine tables: WorkflowInstance, ExecutionPointer, EventSubscription, Event, ScheduledCommand
- OpenIddict tables: umbracoAutomateOpenIddictCredentials

## Target Framework

- .NET 10.0 (`net10.0`)
- Umbraco CMS 17.x (`[17.1.0, 17.999.999)`)
- WorkflowCore 3.9.0
- EF Core 10.x
- Central Package Management via `Directory.Packages.props`

## Coding Standards

These standards apply to all packages in this repository. Sub-project CLAUDE.md files should reference this document for shared conventions.

### Method Naming Conventions

#### Async Methods: `[Action][Entity]Async`

All async service methods MUST follow the pattern `[Action][Entity]Async`:

| Component  | Description                              | Examples                                                                                       |
| ---------- | ---------------------------------------- | ---------------------------------------------------------------------------------------------- |
| **Action** | Verb describing the operation            | `Get`, `Create`, `Update`, `Delete`, `Save`, `Find`, `List`, `Validate`, `Execute`, `Generate` |
| **Entity** | Noun describing what's being operated on | `Automation`, `Action`, `Trigger`, `Provider`, `Step`, `Run`                                   |
| **Async**  | Required suffix for async methods        | Always `Async`                                                                                 |

**Correct Examples:**

```csharp
Task<Automation?> GetAutomationAsync(Guid id, CancellationToken ct);
Task<Automation> CreateAutomationAsync(Automation automation, CancellationToken ct);
Task DeleteAutomationAsync(Guid id, CancellationToken ct);
Task<IEnumerable<AutomationRun>> GetRunsAsync(CancellationToken ct);
Task<PagedResult<Automation>> GetAutomationsPagedAsync(int skip, int take, CancellationToken ct);
```

#### Variations and Qualifiers

Qualifiers like `ByAlias`, `Paged`, `All`, `Default` come after the entity:

```csharp
Task<Automation?> GetAutomationByAliasAsync(string alias, CancellationToken ct);
Task<IEnumerable<Automation>> GetAllAutomationsAsync(CancellationToken ct);
```

### Repository Access Pattern

**Repositories are internal implementation details of their corresponding service.** Only the entity's service class may access its repository directly.

### Extension Methods

All extension methods MUST be placed in the `Umbraco.Automate.Extensions` namespace (or the product-specific equivalent) for ease of discovery via IntelliSense.

## Commit Message Format

All commits should follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>(<scope>): <description>
```

**Valid types:** `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `perf`, `ci`, `revert`, `build`

**Valid scopes:** `core`, `provider`, `action`, `trigger`, `automation`, `step`, `settings`, `ui`, `frontend`, `api`, `deps`, `ci`, `docs`, `release`

**Subject must be sentence-case** - Capitalize the first word after the scope.
