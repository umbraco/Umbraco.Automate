# Umbraco.Automate - CLAUDE.md

Product-specific guidance for the core Umbraco.Automate package.

For shared conventions (coding standards, commit messages, naming), see the [root CLAUDE.md](../CLAUDE.md).

## Project Structure

```
Umbraco.Automate/
├── src/
│   ├── Umbraco.Automate/                    # Meta-package (bundles Startup + StaticAssets)
│   ├── Umbraco.Automate.Core/              # Domain models, services, interfaces
│   ├── Umbraco.Automate.Web/               # Management API (controllers, models)
│   ├── Umbraco.Automate.Web.StaticAssets/   # TypeScript/Lit frontend
│   ├── Umbraco.Automate.Persistence/        # EF Core DbContext, repositories
│   ├── Umbraco.Automate.Persistence.SqlServer/  # SQL Server migrations
│   ├── Umbraco.Automate.Persistence.Sqlite/     # SQLite migrations
│   └── Umbraco.Automate.Startup/            # Umbraco Composer, DI registration
└── tests/
    ├── Umbraco.Automate.Tests.Common/       # Shared test utilities
    ├── Umbraco.Automate.Tests.Unit/         # Unit tests (xUnit + Shouldly + Moq)
    └── Umbraco.Automate.Tests.Integration/  # Integration tests
```

## Build & Test

```bash
dotnet build Umbraco.Automate.slnx
dotnet test Umbraco.Automate.slnx
```

## Key Concepts

This package implements a provider-driven automation system built on WorkflowCore.

See [vocabulary.md](../docs/vocabulary.md) for the complete terminology.

### Domain Model

- **Provider** - Package contributing triggers and actions
- **Action** - Reusable unit of work (`StepBody` / `StepBodyAsync`)
- **Trigger** - Event entry point for an automation
- **Settings** - POCO model driving the config UI via EditableModels infrastructure (`[EditableModelField]`)
- **Automation** - User-defined trigger + steps (`IWorkflow`) with draft/published lifecycle
- **Step** - Configured action instance with input bindings
- **Run** - Single automation execution with per-step tracking
- **Workspace** - Admin-configured container grouping automations, scoping connections and membership
- **Connection** - Named, reusable credential set for external services (extensible type system)
- **Service Account** - Execution identity (`UserKind.Api`) tied to a workspace

### Key Services

| Service | Responsibility |
| --- | --- |
| `IAutomationService` | CRUD + publish/unpublish lifecycle |
| `IWorkspaceService` | Workspace management and membership |
| `IConnectionService` | Connection CRUD |
| `IAutomationRunService` | Run tracking and history |
| `IEntityVersionService` | Version history |

### Runtime & Dispatch

- Custom outbox pattern (`IOutboxStore`, `OutboxDispatcher`, `OutboxHealthCheck`) for reliable trigger dispatch
- `TriggerEventHandler` / `TriggerNotificationHandler` for Umbraco notification integration
- Custom `IPersistenceProvider` for WorkflowCore (avoids EF Core version conflicts)

### Security

- Workspace-based access control with membership checks
- Automate section access authorization policy
- SSRF protection for HTTP actions
- Sensitive field masking for credentials

### Database

- Migration prefix: `UmbracoAutomate_`
- DbContext: `UmbracoAutomateDbContext`
- SQL Server and SQLite supported via EF Core
- Domain tables: Automation, Step, AutomationRun, StepRun, Workspace, Connection, OutboxMessage
- Engine tables: WorkflowInstance, ExecutionPointer, EventSubscription, Event, ScheduledCommand

## Commit Scopes

Use these scopes for conventional commits affecting this product:

`core`, `provider`, `action`, `trigger`, `automation`, `step`, `settings`, `ui`, `frontend`, `api`
