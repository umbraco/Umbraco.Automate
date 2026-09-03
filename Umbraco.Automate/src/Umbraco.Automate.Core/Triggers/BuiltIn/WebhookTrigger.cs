using System.Text.Json;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Built-in trigger that fires when an external system sends an HTTP request to the automation's webhook URL.
/// </summary>
[Trigger(WellKnownAlias, "Webhook",
    Description = "Fires when an HTTP request is received at this automation's webhook URL.",
    Group = "Core",
    Icon = "icon-webhook")]
public sealed class WebhookTrigger : WebhookTriggerBase<WebhookTriggerSettings, WebhookTriggerOutput>, ISupportsManualRun
{
    /// <summary>
    /// Well-known alias for the built-in webhook trigger.
    /// </summary>
    public const string WellKnownAlias = "umbracoAutomate.webhook";

    /// <summary>
    /// Headers every on-demand run carries unless the saved test headers override them.
    /// </summary>
    private static readonly Dictionary<string, string> DefaultTestHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Content-Type"] = "application/json",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="WebhookTrigger"/> class.
    /// </summary>
    public WebhookTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Produces the same shape <c>WebhookEndpointController</c> builds from a real request, via
    /// the same JSON round-trip the dispatch path uses, so a step sees identical data whether it
    /// was exercised on demand or by an external caller. Authentication and the allowed-method
    /// check don't apply here on purpose: the point is exercising the steps, not the signature.
    /// </remarks>
    public ManualRunOutput CreateManualRunOutput(object? settings)
    {
        var typedSettings = settings as WebhookTriggerSettings;

        var headers = ParseTestHeaders(typedSettings?.TestRequestHeaders);
        if (headers is null)
        {
            return ManualRunOutput.Invalid(
                "The webhook trigger's test request headers are not a JSON object of header names to values. "
                + "Fix them in the trigger's settings.");
        }

        var output = new WebhookTriggerOutput
        {
            // The method the webhook accepts, so a GET webhook is exercised as a GET.
            Method = string.IsNullOrWhiteSpace(typedSettings?.AllowedMethod) ? "POST" : typedSettings.AllowedMethod,
            // Verbatim, as the real endpoint does — a step under test may well want to see
            // malformed JSON, so parsing it here would hide the case being tested.
            Body = typedSettings?.TestRequestBody,
            Headers = headers,
        };

        return ManualRunOutput.From(
            JsonOptions.DeserializeToUnwrappedDictionary(JsonSerializer.Serialize(output, JsonOptions.Default)));
    }

    /// <summary>
    /// Parses the saved test headers over <see cref="DefaultTestHeaders"/>. Returns <c>null</c>
    /// when the text is present but isn't a JSON object of strings, so the run is refused rather
    /// than started with headers the author didn't mean.
    /// </summary>
    private static Dictionary<string, string>? ParseTestHeaders(string? json)
    {
        var headers = new Dictionary<string, string>(DefaultTestHeaders, StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(json))
        {
            return headers;
        }

        Dictionary<string, string>? overrides;
        try
        {
            overrides = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions.Default);
        }
        catch (JsonException)
        {
            return null;
        }

        // Explicit "null" parses without throwing but says nothing about the headers wanted.
        if (overrides is null)
        {
            return null;
        }

        foreach (var (key, value) in overrides)
        {
            headers[key] = value;
        }

        return headers;
    }
}
