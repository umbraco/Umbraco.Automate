using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
[Route(Constants.WebhookApi.RoutePath)]
[MapToApi(Constants.WebhookApi.ApiName)]
[ApiExplorerSettings(GroupName = "Webhooks")]
[EnableRateLimiting(Constants.WebhookApi.RateLimitPolicy)]
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
    /// <remarks>
    /// The caller is authenticated before anything about the automation is revealed. An unknown
    /// automation, or one without a webhook trigger, answers 401 like a bad credential, and the
    /// allowed method and publish state are only reported to an authenticated caller.
    /// </remarks>
    [HttpGet("{automationId:guid}")]
    [HttpPost("{automationId:guid}")]
    [HttpPut("{automationId:guid}")]
    [HttpPatch("{automationId:guid}")]
    [HttpDelete("{automationId:guid}")]
    [HttpHead("{automationId:guid}")]
    [DisableFormValueModelBinding]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status405MethodNotAllowed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ReceiveWebhook(
        Guid automationId,
        CancellationToken cancellationToken)
    {
        // Validate payload size before anything else, so the answer is the same for every automation.
        var maxPayloadBytes = _webhookOptions.Value.MaxPayloadBytes;
        if (Request.ContentLength > maxPayloadBytes)
        {
            return PayloadTooLarge(maxPayloadBytes);
        }

        var automation = await _automationService.GetAutomationAsync(automationId, cancellationToken);
        var triggerAlias = automation?.Trigger?.TriggerAlias;
        var trigger = triggerAlias is not null ? _triggers.GetByAlias<WebhookTrigger>(triggerAlias) : null;
        if (automation is null || triggerAlias is null || trigger is null)
        {
            return Unauthorized();
        }

        // Resolve trigger settings (resolves $ConfigKey references via the trigger's resolver).
        var triggerSettings = automation.Trigger?.Settings != null
            ? trigger.ResolveSettings(automation.Trigger.Settings)
            : null;

        var authenticator = ResolveAuthenticator(triggerSettings);
        var authenticatorSettings = authenticator is not null
            ? ResolveAuthenticatorSettings(authenticator, triggerSettings?.Authenticator)
            : null;

        // Run pre-body authentication for authenticators that don't need the body.
        // Lets large-payload spam fail fast with 401 before we read into memory.
        var authenticated = false;
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

            authenticated = true;
        }

        // Read the request body with size-limited stream. A chunked request has no Content-Length,
        // so only a declared length of zero means there is nothing to read.
        string? body = null;
        if (Request.ContentLength is not 0)
        {
            // Umbraco's routing parses a form-encoded body before the action runs, which leaves the
            // stream at the end. The body is buffered for this route, so rewind it to read it raw.
            if (Request.Body.CanSeek)
            {
                Request.Body.Position = 0;
            }

            Request.Body = new LimitedStream(Request.Body, maxPayloadBytes);
            using var reader = new StreamReader(Request.Body);

            try
            {
                body = await reader.ReadToEndAsync(cancellationToken);
            }
            catch (InvalidOperationException)
            {
                return PayloadTooLarge(maxPayloadBytes);
            }
            catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
            {
                // The server's own limit, set before routing, tripped first.
                return PayloadTooLarge(maxPayloadBytes);
            }

            if (body.Length == 0)
            {
                body = null;
            }
        }

        // Post-body authentication for strategies that need the body (e.g. HMAC).
        if (authenticator is not null && !authenticated)
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

        var allowedMethod = string.IsNullOrEmpty(triggerSettings?.AllowedMethod) ? "POST" : triggerSettings.AllowedMethod;
        if (!string.Equals(allowedMethod, Request.Method, StringComparison.OrdinalIgnoreCase))
        {
            return StatusCode(StatusCodes.Status405MethodNotAllowed, new ProblemDetails
            {
                Title = "Method not allowed",
                Detail = $"This webhook accepts: {allowedMethod}",
                Status = StatusCodes.Status405MethodNotAllowed,
            });
        }

        if (automation.Status != AutomationStatus.Published)
        {
            return Conflict(new ProblemDetails
            {
                Title = "Automation not active",
                Detail = "The automation must be published to receive webhooks.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        // Leave the credential out of the output: it is stored with the run and readable by steps.
        var credentialHeaders = authenticator?.CredentialHeaderNames ?? [];
        var credentialQueryParameters = authenticator?.CredentialQueryParameterNames ?? [];

        var output = new WebhookTriggerOutput
        {
            Method = Request.Method,
            Body = body,
            Headers = Request.Headers
                .Where(h => !h.Key.StartsWith(":", StringComparison.Ordinal))
                .Where(h => !credentialHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(h => h.Key, h => h.Value.ToString()),
            Query = Request.Query
                .Where(q => !credentialQueryParameters.Contains(q.Key, StringComparer.OrdinalIgnoreCase))
                .ToDictionary(q => q.Key, q => q.Value.ToString()),
        };

        _logger.LogInformation(
            "Webhook received for automation {AutomationId} ({AutomationAlias})",
            automationId, automation.Alias);

        // Target this exact automation. The endpoint is addressed by automation ID, so the
        // dispatch must not fan out to every published automation sharing the webhook alias.
        await _dispatcher.DispatchAsync(
            new TriggerEvent<WebhookTriggerOutput>
            {
                TriggerAlias = triggerAlias,
                TargetAutomationId = automationId,
                InitiatorType = TriggerInitiatorType.Webhook,
                Output = output,
            },
            cancellationToken);

        return Accepted();
    }

    private ObjectResult PayloadTooLarge(long maxPayloadBytes)
        => StatusCode(StatusCodes.Status413PayloadTooLarge, new ProblemDetails
        {
            Title = "Payload too large",
            Detail = $"Maximum webhook payload size is {maxPayloadBytes} bytes.",
            Status = StatusCodes.Status413PayloadTooLarge,
        });

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
