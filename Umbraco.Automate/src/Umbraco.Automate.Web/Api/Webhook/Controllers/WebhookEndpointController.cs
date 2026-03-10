using System.Security.Cryptography;
using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Api.Common.Filters;

namespace Umbraco.Automate.Web.Api.Webhook.Controllers;

/// <summary>
/// Public endpoint for receiving incoming webhooks that trigger automations.
/// Authenticated via a per-trigger secret (<c>X-Webhook-Secret</c> header or <c>secret</c> query param).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("/umbraco/automate/api/v{version:apiVersion}/webhook")]
[MapToApi(Constants.WebhookApi.ApiName)]
[JsonOptionsName(Constants.WebhookApi.ApiName)]
public sealed class WebhookEndpointController : ControllerBase
{
    internal const string SecretHeaderName = "X-Webhook-Secret";
    internal const string SecretQueryParam = "secret";
    internal const string SecretSettingsKey = "secret";

    private readonly IAutomationService _automationService;
    private readonly ITriggerDispatcher _dispatcher;
    private readonly TriggerCollection _triggers;
    private readonly ILogger<WebhookEndpointController> _logger;

    /// <inheritdoc cref="WebhookEndpointController"/>
    public WebhookEndpointController(
        IAutomationService automationService,
        ITriggerDispatcher dispatcher,
        TriggerCollection triggers,
        ILogger<WebhookEndpointController> logger)
    {
        _automationService = automationService;
        _dispatcher = dispatcher;
        _triggers = triggers;
        _logger = logger;
    }

    /// <summary>
    /// Receives an incoming webhook request and triggers the matching automation.
    /// Requires a valid secret via <c>X-Webhook-Secret</c> header or <c>secret</c> query parameter.
    /// </summary>
    [HttpPost("{automationId:guid}")]
    [HttpPut("{automationId:guid}")]
    [HttpPatch("{automationId:guid}")]
    [HttpDelete("{automationId:guid}")]
    [MapToApiVersion("1.0")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReceiveWebhook(
        Guid automationId,
        CancellationToken cancellationToken)
    {
        var automation = await _automationService.GetAutomationAsync(automationId, cancellationToken);
        if (automation is null)
        {
            return NotFound();
        }

        if (automation.Status != AutomationStatus.Published || !automation.IsEnabled)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Automation not active",
                Detail = "The automation must be published and enabled to receive webhooks.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        var triggerAlias = automation.Trigger?.TriggerAlias;
        if (triggerAlias is null)
        {
            return NotFound();
        }

        // Verify the automation's trigger is a webhook trigger and accepts this HTTP method.
        var trigger = _triggers.FirstOrDefault(t => t.Alias == triggerAlias);
        if (trigger is not IWebhookTrigger webhookTrigger)
        {
            return NotFound();
        }

        // Validate the webhook secret.
        var expectedSecret = GetSecretFromSettings(automation.Trigger);
        if (!string.IsNullOrEmpty(expectedSecret) && !ValidateSecret(expectedSecret))
        {
            return Unauthorized();
        }

        if (!webhookTrigger.AllowedMethods.Contains(Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status405MethodNotAllowed, new ProblemDetails
            {
                Title = "Method not allowed",
                Detail = $"This webhook accepts: {string.Join(", ", webhookTrigger.AllowedMethods)}",
                Status = StatusCodes.Status405MethodNotAllowed,
            });
        }

        // Read the request body.
        string? body = null;
        if (Request.ContentLength is > 0)
        {
            using var reader = new StreamReader(Request.Body);
            body = await reader.ReadToEndAsync(cancellationToken);
        }

        var output = new WebhookTriggerOutput
        {
            Method = Request.Method,
            Body = body,
            Headers = Request.Headers
                .Where(h => !h.Key.StartsWith(":", StringComparison.Ordinal))
                .ToDictionary(h => h.Key, h => h.Value.ToString()),
            Query = Request.Query
                .ToDictionary(q => q.Key, q => q.Value.ToString()),
        };

        _logger.LogInformation(
            "Webhook received for automation {AutomationId} ({AutomationAlias})",
            automationId, automation.Alias);

        await _dispatcher.DispatchAsync(
            new TriggerEvent<WebhookTriggerOutput>
            {
                TriggerAlias = triggerAlias,
                InitiatorType = "webhook",
                Output = output,
            },
            cancellationToken);

        return Accepted();
    }

    private static string? GetSecretFromSettings(TriggerConfiguration? trigger)
    {
        if (trigger?.Settings.TryGetValue(SecretSettingsKey, out var value) is true)
        {
            return value?.ToString();
        }

        return null;
    }

    private bool ValidateSecret(string expectedSecret)
    {
        // Check header first, then query param fallback.
        var providedSecret = Request.Headers[SecretHeaderName].FirstOrDefault()
                             ?? Request.Query[SecretQueryParam].FirstOrDefault();

        if (string.IsNullOrEmpty(providedSecret))
        {
            return false;
        }

        // Constant-time comparison to prevent timing attacks.
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expectedSecret),
            System.Text.Encoding.UTF8.GetBytes(providedSecret));
    }
}
