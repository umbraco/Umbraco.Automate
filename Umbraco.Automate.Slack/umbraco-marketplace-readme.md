## Umbraco.Automate.Slack

Slack connection and actions for Umbraco Automate - post messages to Slack channels from your automations.

### Features

- **Slack Connection Type** - OAuth-based connection, authorized and managed from the backoffice
- **Send Message Action** - Post messages to Slack channels as steps in automations (e.g. notify a channel when content is published)
- **Automatic Token Management** - OAuth credentials are stored and refreshed transparently

### Configuration

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

Add the OAuth callback URI `{your-site}/umbraco/automate/oauth/callback/slack` to your Slack app's redirect URLs. Then create a Slack connection in a workspace from the backoffice and authorize it via the OAuth popup.

### Requirements

- Umbraco CMS 17.4.0+
- Umbraco.Automate 17.0.0+
- .NET 10.0
- A Slack app with client ID and secret
