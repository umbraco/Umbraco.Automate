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

All long-term branches are **version-prefixed** with `vN`, where `N` is the CMS major (e.g. `v18`). The `claude/` prefix is exempt (auto-created by Claude Code).

| Pattern                   | Description                          | Example                       |
| ------------------------- | ------------------------------------ | ----------------------------- |
| `vN/dev`                  | Active development for version N     | `v18/dev`                     |
| `vN/main`                 | Last released state for version N    | `v18/main`                    |
| `vN/feature/<anything>`   | Feature or fix branch for version N  | `v18/feature/add-triggers`    |
| `vN/release/<anything>`   | Release preparation for version N    | `v18/release/2026.03.1`       |
| `vN/hotfix/<anything>`    | Emergency fixes for version N        | `v17/hotfix/2026.03.1`        |
| `claude/<anything>`       | Claude Code automation (exempt)      | `claude/fix-thing`            |

### Active Version Lines

| CMS Version | Type | Branches                  | Policy                |
| ----------- | ---- | ------------------------- | --------------------- |
| v18         | STS  | `v18/dev` / `v18/main`    | Features + bug fixes  |
| v17         | LTS  | `v17/dev` / `v17/main`    | Features + bug fixes  |

A new CMS major ships via the `/post-release-cleanup` skill, which creates `v(N+1)/dev` and `v(N+1)/main` and updates the GitHub default branch.

### Recommended Naming Conventions

**Release branches:** `vN/release/YYYY.MM.N`
**Hotfix branches:** `vN/hotfix/YYYY.MM.N`
**Feature branches:** `vN/feature/<descriptive-name>`

## Development Workflow

### Feature Development

```bash
# 1. Create feature branch from the target version line's dev
git checkout v18/dev
git pull origin v18/dev
git checkout -b v18/feature/add-triggers

# 2. Make changes in the product directory
# Edit: Umbraco.Automate/src/Umbraco.Automate.Core/...

# 3. Build and test
dotnet build Umbraco.Automate/Umbraco.Automate.slnx
dotnet test Umbraco.Automate/Umbraco.Automate.slnx

# 4. Commit changes
git add .
git commit -m "feat(trigger): Add content published trigger"

# 5. Push and create PR targeting v18/dev
git push -u origin v18/feature/add-triggers
```

To backport a fix to an older line, branch from that line's dev instead (e.g. `git checkout v17/dev`, then `git checkout -b v17/feature/<name>`) and open the PR against `v17/dev`. Each version line is independent — do not forward-merge one line's `dev` into a newer line's `dev`.

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

- [ ] Branch name follows convention (`vN/feature/<anything>`)
- [ ] PR targets the correct `vN/dev` base branch
- [ ] Code follows coding standards (see CLAUDE.md)
- [ ] All tests pass
- [ ] Frontend builds (if frontend changes)
- [ ] Documentation updated (if needed)

## Release Process

Each product is versioned and released independently using Nerdbank.GitVersioning (NBGV).

### Release Workflow

1. Create release branch from the version line's dev: `vN/release/YYYY.MM.N`
2. Update `version.json` for each product
3. Generate changelogs
4. Push release branch
5. CI builds and publishes packages
6. Merge back into `vN/main` and `vN/dev` after testing (via the `/post-release-cleanup` skill)

## Coding Standards

All contributions must follow the [coding standards in CLAUDE.md](CLAUDE.md#coding-standards).

## License

By contributing, you agree that your contributions will be licensed under the same license as the Umbraco.Automate project (MIT).
