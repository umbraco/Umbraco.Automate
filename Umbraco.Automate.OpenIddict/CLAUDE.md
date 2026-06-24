# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **Note:** This is the Umbraco.Automate.OpenIddict package. See the [root CLAUDE.md](../CLAUDE.md) for shared coding standards, build commands, and repository-wide conventions.

## Build Commands

```bash
# Build the solution
dotnet build Umbraco.Automate.OpenIddict.slnx

# Run tests
dotnet test Umbraco.Automate.OpenIddict.slnx
```

## Architecture Overview

Umbraco.Automate.OpenIddict provides reusable OAuth connection infrastructure for Umbraco Automate using OpenIddict Client WebIntegration. It enables provider packages (Slack, GitHub, etc.) to add OAuth flows with minimal boilerplate.

### Project Structure

| Project | Purpose |
| --- | --- |
| `Umbraco.Automate.OpenIddict` | Core RCL: domain model, services, controllers, persistence, property editor |
| `Umbraco.Automate.OpenIddict.Persistence.SqlServer` | SQL Server EF Core migrations |
| `Umbraco.Automate.OpenIddict.Persistence.Sqlite` | SQLite EF Core migrations |

### Key Concepts

- **OAuthCredentials** — Stored OAuth tokens (access, refresh, expiry, scopes) for a single provider authorization
- **OAuthConnectionTypeBase** — Abstract base class for OAuth-enabled connection types; provider packages extend this
- **IOAuthProviderConfigurationSource** — Replaceable source for provider app credentials (client ID/secret). Default reads from `IConfiguration` at `Umbraco:Automate:Providers:{providerName}`
- **OpenIddictClientCredentialsConfigurator** — `IPostConfigureOptions<OpenIddictClientOptions>` that patches credentials and redirect URIs onto OpenIddict registrations at runtime via `IOAuthProviderConfigurationSource`
- **OAuthChallengeController** — Initiates the OAuth popup flow (requires backoffice auth)
- **OAuthCallbackController** — Handles provider redirects, exchanges codes for tokens, stores credentials
- **IOAuthCredentialsService** — Service for credentials CRUD and transparent token refresh
- **OAuth Property Editor** (`Umb.Automate.OAuth`) — Lit component handling the popup OAuth flow UI

### How It Works

1. Provider packages register their OpenIddict WebIntegration provider at startup (e.g. `.AddSlack(_ => { })`) — no credentials needed
2. `OpenIddictClientCredentialsConfigurator` patches client ID/secret from `IOAuthProviderConfigurationSource` and sets the callback URI from convention (`automate/oauth/callback/{provider}`)
3. Connection settings reference an `OAuthCredentialsId` via the OAuth property editor
4. The challenge controller redirects to the provider's authorize page in a popup
5. The callback controller exchanges the auth code for tokens and stores them as `OAuthCredentials`
6. At runtime, actions call `GetValidAccessTokenAsync()` which handles refresh transparently

### Database

- Migration prefix: `UmbracoAutomateOpenIddict_`
- DbContext: `OpenIddictDbContext`
- Tables: `umbracoAutomateOpenIddictCredentials`

## Key Namespaces

- `Umbraco.Automate.OpenIddict.Credentials` — Domain model, service interface
- `Umbraco.Automate.OpenIddict.Credentials.Persistence` — EF Core DbContext, entities, factory, repository
- `Umbraco.Automate.OpenIddict.ConnectionTypes` — OAuth connection type base class
- `Umbraco.Automate.OpenIddict.Controllers` — OAuth challenge and callback endpoints
- `Umbraco.Automate.OpenIddict.Providers` — Provider configuration source, credentials configurator
- `Umbraco.Automate.OpenIddict.Configuration` — Composer and DI registration
- `Umbraco.Automate.OpenIddict.Extensions` — Extension methods (`AddAutomateOpenIddict`)

## Dependencies

- Umbraco CMS 18.x
- Umbraco.Automate.Core
- OpenIddict.Client.WebIntegration 7.5.x

## Commit Scopes

Use these scopes for conventional commits affecting this package:

`openiddict`, `oauth`
