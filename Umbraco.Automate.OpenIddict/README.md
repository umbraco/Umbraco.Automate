# Umbraco Automate OpenIddict

OAuth connection support for Umbraco Automate using OpenIddict Client WebIntegration.

## Overview

Umbraco.Automate.OpenIddict provides reusable OAuth infrastructure for Umbraco Automate provider packages. It handles the full OAuth authorization code flow — challenge, callback, token storage, and automatic refresh — so provider packages (Slack, GitHub, etc.) can add OAuth connectivity with minimal boilerplate.

## Key Features

- **OpenIddict Client WebIntegration** — leverages the OpenIddict web provider ecosystem
- **Automatic token management** — stores OAuth credentials and transparently refreshes expired tokens
- **Convention-based callbacks** — redirect URIs follow `automate/oauth/callback/{provider}` convention
- **Pluggable credential source** — client ID/secret resolved via `IOAuthProviderConfigurationSource` (default: `IConfiguration`)
- **OAuth property editor** — Lit-based UI component for the backoffice OAuth popup flow

## Installation

```bash
dotnet add package Umbraco.Automate.OpenIddict
```

This meta-package includes:

- `Umbraco.Automate.OpenIddict.Core` — domain model, services, controllers, property editor
- `Umbraco.Automate.OpenIddict.Persistence.SqlServer` — SQL Server EF Core migrations
- `Umbraco.Automate.OpenIddict.Persistence.Sqlite` — SQLite EF Core migrations

## Configuration

Provider credentials are configured via `appsettings.json`:

```json
{
  "Umbraco": {
    "Automate": {
      "Providers": {
        "ProviderName": {
          "ClientId": "your-client-id",
          "ClientSecret": "your-client-secret"
        }
      }
    }
  }
}
```

## License

MIT - See [LICENSE](../LICENSE) for details.
