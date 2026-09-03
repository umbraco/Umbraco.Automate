using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Umbraco.Automate.Core;
using Umbraco.Automate.Core.Connections;
using Umbraco.Automate.OpenIddict.ConnectionTypes;
using Umbraco.Automate.OpenIddict.Credentials;

namespace Umbraco.Automate.Slack.Connection;

/// <summary>
/// Connection type for Slack workspaces using OAuth via OpenIddict WebIntegration.
/// </summary>
[ConnectionType("slack", "Slack", Group = "Messaging", Icon = "icon-message", Description = "Connect to a Slack workspace")]
public sealed class SlackConnectionType : OAuthConnectionTypeBase<SlackConnectionSettings>
{
    private readonly IHttpClientFactory _httpClientFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SlackConnectionType"/> class.
    /// </summary>
    public SlackConnectionType(
        ConnectionTypeInfrastructure infrastructure,
        IOAuthCredentialsService credentialsService,
        IHttpClientFactory httpClientFactory)
        : base(infrastructure, credentialsService)
    {
        _httpClientFactory = httpClientFactory;
    }

    /// <inheritdoc />
    public override string ProviderName => "Slack";

    /// <inheritdoc />
    public override string? SetupDocsUrl => "https://docs.umbraco.com/umbraco-automate/add-ons/slack/installation#create-a-slack-app";

    /// <summary>
    /// Adds a Slack-specific check on top of the base token-resolution check: calls
    /// <c>auth.test</c> to confirm the token actually works against the Slack API and returns
    /// the team name on success.
    /// </summary>
    public override async Task<ConnectionValidationResult> ValidateAsync(
        object? settings,
        CancellationToken cancellationToken)
    {
        // First make sure we can resolve a token — if not, the base already produced a
        // Failure result with the appropriate message, so return it unchanged.
        var baseResult = await base.ValidateAsync(settings, cancellationToken);
        if (baseResult.Status != ConnectionValidationStatus.Success)
        {
            return baseResult;
        }

        var credentialsId = ((SlackConnectionSettings)settings!).OAuthCredentialsId!.Value;
        var token = await CredentialsService.GetValidAccessTokenAsync(credentialsId, cancellationToken);

        using var client = _httpClientFactory.CreateClient(Constants.HttpClients.Default);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        SlackAuthTestResponse? response;
        try
        {
            using var httpResponse = await client.PostAsync(
                "https://slack.com/api/auth.test",
                content: null,
                cancellationToken);

            response = await httpResponse.Content.ReadFromJsonAsync<SlackAuthTestResponse>(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ConnectionValidationResult.Failure(
                "Could not reach the Slack API.",
                [ex.Message]);
        }

        if (response?.Ok != true)
        {
            return ConnectionValidationResult.Failure(
                $"Slack rejected the access token: {response?.Error ?? "unknown error"}.");
        }

        var team = response.Team ?? "your Slack workspace";
        var user = response.User is null ? string.Empty : $" as @{response.User}";
        return ConnectionValidationResult.Success($"Connected to {team}{user}.");
    }

    private sealed class SlackAuthTestResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("team")]
        public string? Team { get; set; }

        [JsonPropertyName("user")]
        public string? User { get; set; }
    }
}
