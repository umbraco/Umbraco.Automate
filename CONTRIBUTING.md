# Contributing to Umbraco.Automate

This guide explains how to contribute to the Umbraco.Automate monorepo, covering branch naming conventions, git workflows, and release processes.

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- Node.js 20.x
- Git
- SQL Server or SQLite (for database development)
- IDE: Visual Studio 2022, VS Code, or JetBrains Rider

### Initial Setup

```bash
# Clone the repository
git clone https://github.com/umbraco/Umbraco.Automate.git
cd Umbraco.Automate

# Build
dotnet build Umbraco.Automate/Umbraco.Automate.slnx
```

### Repository Structure

```
Umbraco.Automate/              # Monorepo root
├── Umbraco.Automate/          # Core automation package
└── docs/                      # Shared documentation
```

## Branch Naming Convention

**All branches MUST follow these patterns.**

### Valid Branch Patterns

| Pattern              | Description                | Example                      |
| -------------------- | -------------------------- | ---------------------------- |
| `main`               | Main development branch    | `main`                       |
| `dev`                | Integration branch         | `dev`                        |
| `support/<anything>` | Long-term support branches | `support/1.x`               |
| `feature/<anything>` | New feature development    | `feature/add-triggers`       |
| `release/<anything>` | Release preparation        | `release/2026.03`            |
| `hotfix/<anything>`  | Emergency fixes            | `hotfix/2026.03.1`           |

### Recommended Naming Conventions

**Release branches:** `release/YYYY.MM.N`
**Hotfix branches:** `hotfix/YYYY.MM.N`
**Feature branches:** `feature/<descriptive-name>`

## Development Workflow

### Feature Development

```bash
# 1. Create feature branch from main
git checkout main
git pull origin main
git checkout -b feature/add-triggers

# 2. Make changes in the product directory
# Edit: Umbraco.Automate/src/Umbraco.Automate.Core/...

# 3. Build and test
dotnet build Umbraco.Automate/Umbraco.Automate.slnx
dotnet test Umbraco.Automate/Umbraco.Automate.slnx

# 4. Commit changes
git add .
git commit -m "feat(trigger): Add content published trigger"

# 5. Push and create PR
git push -u origin feature/add-triggers
```

## Commit Message Format

All commits should follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>(<scope>): <description>

Types: feat, fix, docs, chore, refactor, test, perf, ci, revert, build
Scopes: core, provider, action, trigger, automation, step, settings, ui, frontend, api
```

### Rules

1. **Subject must be sentence-case** - Capitalize the first word after the scope
2. **Scope must be valid** - Use one of the allowed scopes
3. **Body lines must not exceed 100 characters**

### What Appears in the Changelog

- `feat:` - New features
- `fix:` - Bug fixes
- `perf:` - Performance improvements
- `BREAKING CHANGE` - Breaking changes

## Pull Request Process

### PR Title Format

Use conventional commits format:

```
feat(core): Add streaming support for automations
fix(trigger): Resolve memory leak in event listener
```

### PR Checklist

- [ ] Branch name follows convention
- [ ] Code follows coding standards (see CLAUDE.md)
- [ ] All tests pass
- [ ] Frontend builds (if frontend changes)
- [ ] Documentation updated (if needed)

## Release Process

Each product is versioned and released independently using Nerdbank.GitVersioning (NBGV).

### Release Workflow

1. Create release branch: `release/YYYY.MM.N`
2. Update `version.json` for each product
3. Generate changelogs
4. Push release branch
5. CI builds and publishes packages
6. Merge to main after testing

## Coding Standards

All contributions must follow the [coding standards in CLAUDE.md](CLAUDE.md#coding-standards).

## License

By contributing, you agree that your contributions will be licensed under the same license as the Umbraco.Automate project (MIT).
