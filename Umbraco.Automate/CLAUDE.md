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
- **Settings** - POCO model driving the config UI
- **Automation** - User-defined trigger + steps (`IWorkflow`)
- **Step** - Configured action instance with input bindings
- **Run** - Single automation execution

### Database

- Migration prefix: `UmbracoAutomate_`
- DbContext: `UmbracoAutomateDbContext`
- SQL Server and SQLite supported via EF Core

## Commit Scopes

Use these scopes for conventional commits affecting this product:

`core`, `provider`, `action`, `trigger`, `automation`, `step`, `settings`, `ui`, `frontend`, `api`
