# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **Note:** This is the Umbraco.Automate.Forms package. See the [root CLAUDE.md](../CLAUDE.md) for shared coding standards, build commands, and repository-wide conventions.

## Build Commands

```bash
# Build the solution
dotnet build Umbraco.Automate.Forms.slnx
```

## Architecture Overview

Umbraco.Automate.Forms is a provider package that adds Umbraco Forms triggers and actions to Umbraco Automate. It listens to Forms notification events and provides programmatic access to form operations.

### Project Structure

Single class library project organized by domain:

```
Umbraco.Automate.Forms/
├── src/Umbraco.Automate.Forms/
│   ├── Triggers/          # Form event triggers (submitted, approved)
│   └── Actions/           # Form actions (submit, export entries)
└── Umbraco.Automate.Forms.slnx
```

### How It Works

1. Triggers use `NotificationTriggerBase` to auto-wire to Forms' `RecordSubmittedNotification` and `RecordApprovedNotification`
2. Actions inject Forms services (`IFormService`, `IRecordService`, `IRecordReaderService`) via DI
3. No connections needed — Forms is in-process, no external authentication required
4. No Composer needed — all types are auto-discovered via `[Trigger]` and `[Action]` attributes

## Dependencies

- Umbraco.Automate.Core
- Umbraco.Forms.Core 17.x

## Commit Scopes

Use these scopes for conventional commits affecting this package:

`provider`
