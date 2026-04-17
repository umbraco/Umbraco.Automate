using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Triggers.Webhooks;
using Umbraco.Automate.Core.Triggers.Webhooks.BuiltIn;
using Umbraco.Automate.Web.Api.Webhook;
using Umbraco.Cms.Api.Common.Attributes;

namespace Umbraco.Automate.Web.Api.Webhook.Controllers;

/// <summary>
/// Public endpoint for receiving incoming webhooks that trigger automations.
/// Each trigger selects an authentication strategy (e.g. plain-secret header, HMAC-SHA256, provider-specific).
/// </summary>
[ApiController]
[Route("automate/webhook")]
[MapToApi(Constants.WebhookApi.ApiName)]
[ApiExplorerSettings(GroupName = "Webhooks")]
public sealed class WebhookEndpointController : ControllerBase
{
    private readonly IAutomationService _automationService;
    private readonly ITriggerDispatcher _dispatcher;
    private readonly TriggerCollection _triggers;
    private readonly WebhookAuthenticatorCollection _authenticators;
    private readonly IEditableModelResolver _modelResolver;
    private readonly IOptions<WebhookOptions> _webhookOptions;
    private readonly ILogger<WebhookEndpointController> _logger;

    /// <inheritdoc cref="WebhookEndpointController"/>
    public WebhookEndpointController(
        IAutomationService automationService,
        ITriggerDispatcher dispatcher,
        TriggerCollection triggers,
        WebhookAuthenticatorCollection authenticators,
        IEditableModelResolver modelResolver,
        IOptions<WebhookOptions> webhookOptions,
        ILogger<WebhookEndpointController> logger)
    {
        _automationService = automationService;
        _dispatcher = dispatcher;
        _triggers = triggers;
        _authenticators = authenticators;
        _modelResolver = modelResolver;
        _webhookOptions = webhookOptions;
        _logger = logger;
    }

    /// <summary>
    /// Receives an incoming webhook request and triggers the matching automation.
    /// Authentication is performed by the strategy configured on the trigger.
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

        var authenticator = ResolveAuthenticator(triggerSettings);
        var authenticatorSettings = authenticator is not null
            ? ResolveAuthenticatorSettings(authenticator, triggerSettings?.Authenticator)
            : null;

        // Run pre-body authentication for authenticators that don't need the body.
        // Lets large-payload spam fail fast with 401 before we read into memory.
        if (authenticator is not null && !authenticator.RequiresBody)
        {
            var preBodyContext = new WebhookAuthenticationContext
            {
                Request = Request,
                Body = null,
            };
            if (!authenticator.Validate(preBodyContext, authenticatorSettings))
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

        // Post-body authentication for strategies that need the body (e.g. HMAC).
        if (authenticator is not null)
        {
            var postBodyContext = new WebhookAuthenticationContext
            {
                Request = Request,
                Body = body,
            };
            if (!authenticator.Validate(postBodyContext, authenticatorSettings))
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
    /// Resolves the authenticator for the trigger. Unknown or missing aliases fall back
    /// to the built-in plain-secret authenticator so stale config never leaves the endpoint
    /// errored; whether the request passes authentication is still up to the strategy.
    /// </summary>
    private IWebhookAuthenticator? ResolveAuthenticator(WebhookTriggerSettings? settings)
    {
        var alias = settings?.Authenticator?.Alias;
        if (!string.IsNullOrEmpty(alias))
        {
            var match = _authenticators.GetByAlias(alias);
            if (match is not null)
            {
                return match;
            }

            _logger.LogWarning(
                "Webhook authenticator '{Alias}' not registered, falling back to '{Fallback}'",
                alias, PlainSecretWebhookAuthenticator.WellKnownAlias);
        }

        return _authenticators.GetByAlias(PlainSecretWebhookAuthenticator.WellKnownAlias);
    }

    private object? ResolveAuthenticatorSettings(IWebhookAuthenticator authenticator, WebhookAuthenticatorConfig? config)
    {
        if (authenticator.SettingsType is null)
        {
            return null;
        }

        var raw = config?.Settings ?? [];
        return _modelResolver.ResolveModel(authenticator.Alias, authenticator.SettingsType, raw, authenticator.GetSettingsSchema());
    }
}
