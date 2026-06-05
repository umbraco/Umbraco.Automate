# Umbraco Automate Slack

Slack connection and actions for Umbraco Automate.

## Overview

Umbraco.Automate.Slack is a provider package that adds Slack connectivity to Umbraco Automate. It contributes a Slack connection type (authenticated via OAuth) and Slack actions that can be used as steps in automations — for example, posting a message to a channel when content is published.

## Key Features

- **Slack connection type** — OAuth-based connection managed in the backoffice, powered by [Umbraco.Automate.OpenIddict](../Umbraco.Automate.OpenIddict/)
- **Send Message action** — post messages to Slack channels from automation steps
- **Automatic token management** — OAuth credentials are stored and refreshed transparently

## Installation

```bash
dotnet add package Umbraco.Automate.Slack
```

## Configuration

Create a [Slack app](https://api.slack.com/apps) and configure its credentials via `appsettings.json`:

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

The OAuth callback URI follows the convention `{your-site}/umbraco/automate/oauth/callback/slack` — add it to your Slack app's redirect URLs.

Once configured, create a Slack connection in a workspace from the backoffice and authorize it via the OAuth popup. Slack actions can then reference the connection.

## License

MIT - See [LICENSE](../LICENSE) for details.
</content>
</invoke>
