using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Web.Api.Webhook;
using Umbraco.Cms.Api.Common.Attributes;

namespace Umbraco.Automate.Web.Api.Webhook.Controllers;

/// <summary>
/// Public endpoint for receiving incoming webhooks that trigger automations.
/// Authenticated via a per-trigger secret (<c>X-Webhook-Secret</c> header or <c>secret</c> query param).
/// </summary>
[ApiController]
[Route("automate/webhook")]
[MapToApi(Constants.WebhookApi.ApiName)]
[ApiExplorerSettings(GroupName = "Webhooks")]
[EnableRateLimiting(Constants.WebhookApi.RateLimitPolicy)]
public sealed class WebhookEndpointController : ControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly ITriggerDispatcher _dispatcher;
    private readonly TriggerCollection _triggers;
    private readonly WebhookAuthenticatorCollection _authenticators;
    private readonly IOptions<WebhookOptions> _webhookOptions;
    private readonly ILogger<WebhookEndpointController> _logger;

    /// <inheritdoc cref="WebhookEndpointController"/>
    public WebhookEndpointController(
        IAutomationService automationService,
        ITriggerDispatcher dispatcher,
        TriggerCollection triggers,
        WebhookAuthenticatorCollection authenticators,
        IOptions<WebhookOptions> webhookOptions,
        ILogger<WebhookEndpointController> logger)
    {
        _automationService = automationService;
        _dispatcher = dispatcher;
        _triggers = triggers;
        _authenticators = authenticators;
        _webhookOptions = webhookOptions;
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
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
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
        var trigger = _triggers.GetByAlias<WebhookTrigger>(triggerAlias);
        if (trigger is null)
        {
            return NotFound();
        }

        // Resolve trigger settings (resolves $ConfigKey references via the trigger's resolver).
        var triggerSettings = automation.Trigger?.Settings != null
            ? trigger.ResolveSettings(automation.Trigger.Settings)
            : null;

        var allowedMethods = triggerSettings?.AllowedMethods ?? ["POST"];
        if (!allowedMethods.Contains(Request.Method, StringComparer.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status405MethodNotAllowed, new ProblemDetails
            {
                Title = "Method not allowed",
                Detail = $"This webhook accepts: {string.Join(", ", allowedMethods)}",
                Status = StatusCodes.Status405MethodNotAllowed,
            });
        }

        // Pre-body authentication for authenticators that don't need the body (e.g. plain-secret).
        // Custom authenticator takes precedence over the built-in ValidateSignature toggle.
        var authenticator = ResolveAuthenticator(triggerSettings);
        if (authenticator is not null
            && !string.IsNullOrEmpty(triggerSettings?.Secret)
            && authenticator.Alias == "plain-secret")
        {
            var preBodyContext = new WebhookAuthenticationContext
            {
                Request = Request,
                Body = null,
                Secret = triggerSettings.Secret,
            };
            if (!authenticator.Validate(preBodyContext))
            {
                return Unauthorized();
            }

            // Already validated — skip post-body check.
            authenticator = null;
        }

        // Validate payload size before reading into memory.
        var maxPayloadBytes = _webhookOptions.Value.MaxPayloadBytes;
        if (Request.ContentLength > maxPayloadBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
            {
                Title = "Payload too large",
                Detail = $"Maximum webhook payload size is {maxPayloadBytes} bytes.",
                Status = StatusCodes.Status413PayloadTooLarge,
            });
        }

        // Read the request body with size-limited stream.
        string? body = null;
        if (Request.ContentLength is > 0)
        {
            Request.Body = new LimitedStream(Request.Body, maxPayloadBytes);
            using var reader = new StreamReader(Request.Body);

            try
            {
                body = await reader.ReadToEndAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
                {
                    Title = "Payload too large",
                    Detail = $"Maximum webhook payload size is {maxPayloadBytes} bytes.",
                    Status = StatusCodes.Status413PayloadTooLarge,
                });
            }

            // Validate JSON structure when content type declares JSON.
            var contentType = Request.ContentType;
            if (contentType is not null
                && contentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                }
                catch (JsonException)
                {
                    return UnprocessableEntity(new ProblemDetails
                    {
                        Title = "Invalid JSON",
                        Detail = "The request body is not valid JSON.",
                        Status = StatusCodes.Status422UnprocessableEntity,
                    });
                }
            }
        }

        // Post-body authentication (HMAC-SHA256 or custom authenticators that need the body).
        if (authenticator is not null && !string.IsNullOrEmpty(triggerSettings?.Secret))
        {
            var postBodyContext = new WebhookAuthenticationContext
            {
                Request = Request,
                Body = body,
                Secret = triggerSettings.Secret,
            };
            if (!authenticator.Validate(postBodyContext))
            {
                return Unauthorized();
            }
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

    /// <summary>
    /// Resolves the appropriate authenticator based on trigger settings.
    /// Custom authenticator alias takes precedence, then falls back to built-in
    /// plain-secret or hmac-sha256 based on the ValidateSignature flag.
    /// </summary>
    private IWebhookAuthenticator? ResolveAuthenticator(WebhookTriggerSettings? settings)
    {
        if (string.IsNullOrEmpty(settings?.Secret))
        {
            return null;
        }

        // Custom authenticator alias takes precedence.
        if (!string.IsNullOrEmpty(settings.AuthenticatorAlias))
        {
            var custom = _authenticators.GetByAlias(settings.AuthenticatorAlias);
            if (custom is null)
            {
                _logger.LogWarning(
                    "Webhook authenticator '{Alias}' not found, falling back to built-in",
                    settings.AuthenticatorAlias);
            }
            else
            {
                return custom;
            }
        }

        // Fall back to built-in based on ValidateSignature toggle.
        return settings.ValidateSignature
            ? _authenticators.GetByAlias("hmac-sha256")
            : _authenticators.GetByAlias("plain-secret");
    }
}
