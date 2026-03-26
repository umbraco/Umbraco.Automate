using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
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
public sealed class WebhookEndpointController : ControllerBase
{
    internal const string SecretHeaderName = "X-Webhook-Secret";
    internal const string SecretQueryParam = "secret";
    internal const string SignatureHeaderName = "X-Webhook-Signature";

    private readonly IAutomationService _automationService;
    private readonly ITriggerDispatcher _dispatcher;
    private readonly TriggerCollection _triggers;
    private readonly IOptions<WebhookOptions> _webhookOptions;
    private readonly ILogger<WebhookEndpointController> _logger;

    /// <inheritdoc cref="WebhookEndpointController"/>
    public WebhookEndpointController(
        IAutomationService automationService,
        ITriggerDispatcher dispatcher,
        TriggerCollection triggers,
        IOptions<WebhookOptions> webhookOptions,
        ILogger<WebhookEndpointController> logger)
    {
        _automationService = automationService;
        _dispatcher = dispatcher;
        _triggers = triggers;
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

        // Plain secret validation (when signature mode is off).
        // Checked before body reading since it doesn't need the body.
        if (!string.IsNullOrEmpty(triggerSettings?.Secret)
            && !triggerSettings.ValidateSignature
            && !ValidateSecret(triggerSettings.Secret))
        {
            return Unauthorized();
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

        // HMAC-SHA256 signature validation (when signature mode is on).
        // Must happen after body reading since the signature covers the body.
        if (!string.IsNullOrEmpty(triggerSettings?.Secret)
            && triggerSettings.ValidateSignature
            && !ValidateSignature(triggerSettings.Secret, body ?? string.Empty))
        {
            return Unauthorized();
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

    private bool ValidateSignature(string secret, string body)
    {
        // Expect header format: "sha256=<hex>"
        var header = Request.Headers[SignatureHeaderName].FirstOrDefault();
        if (string.IsNullOrEmpty(header) || !header.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedHex = header["sha256=".Length..];

        // Validate hex length before parsing (SHA-256 = 64 hex chars).
        if (providedHex.Length != 64)
        {
            return false;
        }

        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(providedHex);
        }
        catch (FormatException)
        {
            return false;
        }

        var expectedBytes = ComputeHmacSha256Bytes(body, secret);

        // Constant-time comparison on raw bytes to prevent timing attacks.
        return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
    }

    private static byte[] ComputeHmacSha256Bytes(string payload, string secret)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(secret);
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(payload);
        return HMACSHA256.HashData(keyBytes, payloadBytes);
    }
}
