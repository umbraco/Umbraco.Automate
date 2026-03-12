# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Structure

This is a monorepo containing Umbraco.Automate and its add-on packages:

**Core:**

| Product                  | Description                                        | Location             |
| ------------------------ | -------------------------------------------------- | -------------------- |
| **Umbraco.Automate**     | Core automation system for Umbraco CMS             | `Umbraco.Automate/`  |

Each product has its own solution file, CLAUDE.md, and can be built independently. For detailed guidance on a specific product, see its CLAUDE.md file.

## Development Environment

### Prerequisites

- .NET 10.0 SDK
- Node.js 20.x
- Git

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
- **Settings** - POCO models on actions/triggers that drive the config UI
- **Automations** - User-defined trigger + steps sequences
- **Steps** - Configured instances of actions within an automation
- **Inputs/Outputs** - Runtime data flowing between steps
- **Filters** - Conditional logic controlling step execution
- **Runs** - Single executions of an automation

Built on [WorkflowCore](https://github.com/danielgerlag/workflow-core) with a provider-driven extensibility model.

See [docs/vocabulary.md](docs/vocabulary.md) for the complete terminology reference.

## Key Files

| File                                    | Purpose                                                |
| --------------------------------------- | ------------------------------------------------------ |
| `<Product>/version.json`                | Per-product version (Nerdbank.GitVersioning)           |
| `<Product>/changelog.config.json`       | Per-product scopes for changelog generation            |
| `<Product>/CHANGELOG.md`               | Per-product changelog (auto-generated from git history)|

## Database

- SQL Server and SQLite supported via EF Core
- Each product has its own migrations with prefix: `UmbracoAutomate_`

## Target Framework

- .NET 10.0 (`net10.0`)
- Umbraco CMS 17.x
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
