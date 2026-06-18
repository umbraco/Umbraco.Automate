# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

> **Note:** This is the Umbraco.Automate.Slack package. See the [root CLAUDE.md](../CLAUDE.md) for shared coding standards, build commands, and repository-wide conventions.

## Build Commands

```bash
# Build the solution
dotnet build Umbraco.Automate.Slack.slnx
```

## Architecture Overview

Umbraco.Automate.Slack is a provider package that adds Slack connectivity to Umbraco Automate. It uses the OpenIddict package for OAuth authentication and provides Slack-specific actions.

### Project Structure

Single RCL project organized by domain:

```
Umbraco.Automate.Slack/
├── src/Umbraco.Automate.Slack/
│   ├── Actions/           # Slack actions (Send Message, etc.)
│   ├── Configuration/     # Composer (OpenIddict provider registration)
│   └── Connection/        # Connection type and settings
└── Umbraco.Automate.Slack.slnx
```

### How It Works

1. `SlackComposer` registers the Slack provider with OpenIddict WebIntegration — credentials and callback URI are applied automatically by the OpenIddict package
2. `SlackConnectionType` defines the Slack connection type using `OAuthConnectionTypeBase`
3. `SlackConnectionSettings` holds the `OAuthCredentialsId` linking to stored tokens
4. Actions (e.g. `SendMessageAction`) resolve the access token via `IOAuthCredentialsService` and call the Slack Web API

### Configuration

Provider credentials are configured via `appsettings.json`:

```json
{
  "Umbraco": {
    "Automate": {
      "Providers": {
        "Slack": {
          "ClientId": "your-slack-app-client-id",
          "ClientSecret": "your-slack-app-client-secret"
        }
      }
    }
  }
}
```

## Dependencies

- Umbraco.Automate.Core
- Umbraco.Automate.OpenIddict
- OpenIddict.Client.WebIntegration 7.5.x

## Commit Scopes

Use these scopes for conventional commits affecting this package:

`provider`
