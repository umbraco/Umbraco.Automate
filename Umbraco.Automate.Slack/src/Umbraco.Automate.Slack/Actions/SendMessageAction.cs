using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.OpenIddict.Credentials;
using Umbraco.Automate.Slack.Connection;

namespace Umbraco.Automate.Slack.Actions;

/// <summary>
/// Posts a message to a Slack channel using the Slack Web API.
/// Requires a Slack connection with the <c>chat:write</c> scope.
/// </summary>
[Action("slack.sendMessage", "Send Slack Message",
    Description = "Posts a message to a Slack channel.",
    Group = "Messaging",
    Icon = "icon-message")]
public sealed class SendMessageAction : ActionBase<SendMessageSettings>
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOAuthCredentialsService _credentialsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SendMessageAction"/> class.
    /// </summary>
    public SendMessageAction(
        ActionInfrastructure infrastructure,
        IHttpClientFactory httpClientFactory,
        IOAuthCredentialsService credentialsService)
        : base(infrastructure)
    {
        _httpClientFactory = httpClientFactory;
        _credentialsService = credentialsService;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<SendMessageSettings>();

        if (string.IsNullOrWhiteSpace(settings.Channel))
        {
            return ActionResult.Failed(
                new ArgumentException("Channel is required."),
                StepRunErrorCategory.Validation);
        }

        if (string.IsNullOrWhiteSpace(settings.Message))
        {
            return ActionResult.Failed(
                new ArgumentException("Message is required."),
                StepRunErrorCategory.Validation);
        }

        // Resolve the OAuth access token from the connection.
        var connection = context.Connection
            ?? throw new InvalidOperationException("A Slack connection is required to send messages.");

        var connectionSettings = connection.GetSettings<SlackConnectionSettings>();
        if (connectionSettings.OAuthCredentialsId is not { } credentialId)
        {
            return ActionResult.Failed(
                new InvalidOperationException("Slack workspace is not authenticated."),
                StepRunErrorCategory.Authentication);
        }

        var accessToken = await _credentialsService.GetValidAccessTokenAsync(credentialId, cancellationToken);
        if (accessToken is null)
        {
            return ActionResult.Failed(
                new InvalidOperationException("Slack access token is expired or revoked. Please re-authenticate."),
                StepRunErrorCategory.Authentication);
        }

        // Call the Slack chat.postMessage API.
        using var client = _httpClientFactory.CreateClient("UmbracoAutomate");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var payload = new { channel = settings.Channel, text = settings.Message };
        using var response = await client.PostAsJsonAsync(
            "https://slack.com/api/chat.postMessage", payload, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<SlackApiResponse>(cancellationToken);

        if (result?.Ok != true)
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Slack API error: {result?.Error ?? "unknown"}"),
                StepRunErrorCategory.InvalidResponse);
        }

        return ActionResult.Success(new
        {
            result.Channel,
            result.Ts,
        });
    }

    private sealed class SlackApiResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("channel")]
        public string? Channel { get; set; }

        [JsonPropertyName("ts")]
        public string? Ts { get; set; }
    }
}
