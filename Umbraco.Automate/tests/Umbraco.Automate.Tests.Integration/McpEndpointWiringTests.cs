using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.ControlFlow;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Core.Versioning;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web;
using Umbraco.Automate.Web.Api.Mcp;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// The one integration test in this repo that spins up a real ASP.NET Core pipeline
/// (<see cref="TestServer"/>) rather than a hand-built <see cref="IServiceProvider"/> — needed to
/// prove that the MCP middleware, routing, rate limiting and the MCP server's
/// <c>ConfigureSessionOptions</c> callback actually compose. The host is put together from the
/// same <see cref="McpEndpointWiring"/> methods <c>AddUmbracoAutomateMcpApi</c> registers, in the
/// order Umbraco's pipeline runs them, so a change to the production wiring is tested here too.
/// Deliberately kept to this one test class rather than retrofitted onto the rest of the suite.
/// </summary>
public sealed class McpEndpointWiringTests : IAsyncLifetime
{
    // Low enough that a test can reach them, high enough that the other tests never do.
    private const int RateLimitPerMinute = 3;
    private const long MaxPayloadBytes = 4096;

    private IHost _host = null!;
    private HttpClient _client = null!;
    private Guid _automationId;

    public async Task InitializeAsync()
    {
        _automationId = Guid.NewGuid();
        Automation automation = new AutomationBuilder()
            .WithId(_automationId)
            .WithStatus(AutomationStatus.Published)
            .WithTrigger(McpTrigger.WellKnownAlias, new Dictionary<string, object?>
            {
                ["toolName"] = "Echo",
                ["toolDescription"] = "Echoes input back.",
                ["inputFields"] = new List<Dictionary<string, object?>>
                {
                    new()
                    {
                        ["name"] = "message",
                        ["type"] = "Text",
                        ["description"] = "The message to echo.",
                        ["required"] = true,
                    },
                },
            });

        var automationService = new Mock<IAutomationService>();
        automationService.Setup(s => s.GetAutomationAsync(_automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var versionService = new Mock<IEntityVersionService>();
        versionService.Setup(s => s.GetVersionSnapshotAsync<Automation>(_automationId, automation.PublishedVersion!.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync(automation);

        var executor = new Mock<IAutomationExecutor>();
        var runId = Guid.NewGuid();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<Automation>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>?>()))
            .ReturnsAsync(runId);

        var runService = new Mock<IAutomationRunService>();
        runService.Setup(s => s.GetRunStatusAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AutomationRunStatus.Completed);
        runService.Setup(s => s.GetRunAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                AutomationId = _automationId,
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = TriggerInitiatorType.AiAgent,
                Status = AutomationRunStatus.Completed,
            });

        // A real resolver (not a bare Mock.Of<IEditableModelResolver>()) is required here: an
        // unconfigured mock returns null from ResolveModel<T>, which would make the auth
        // middleware refuse every call as having no tool name. Matches the resolver setup used by
        // McpAuthenticationMiddlewareTests / WebhookEndpointControllerTests for the same reason.
        var modelResolver = new EditableModelResolver(new ConfigurationReferenceResolver(new ConfigurationBuilder().Build()));
        var triggers = new TriggerCollection(() => [new McpTrigger(new TriggerInfrastructure(modelResolver))]);

        _host = await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddLogging();
                    services.Configure<McpOptions>(options =>
                    {
                        options.RateLimitPerMinute = RateLimitPerMinute;
                        options.MaxPayloadBytes = MaxPayloadBytes;
                    });
                    services.AddSingleton(automationService.Object);
                    services.AddSingleton(versionService.Object);
                    services.AddSingleton(executor.Object);
                    services.AddSingleton(runService.Object);
                    services.AddSingleton<IRunDataSanitizer>(new RunDataSanitizer(
                        new ActionCollection(() => []),
                        new ControlFlowCollection(() => []),
                        NullLogger<RunDataSanitizer>.Instance));
                    services.AddSingleton(triggers);
                    services.AddMcpServer().WithHttpTransport(options =>
                        McpEndpointWiring.ConfigureHttpTransport(options, initialPollInterval: TimeSpan.FromMilliseconds(1)));

                    // Production sets the rejection status on the webhook's rate limiter
                    // registration, which also installs UseRateLimiter() for both endpoints.
                    services.AddRateLimiter(options =>
                    {
                        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                        McpEndpointWiring.AddRateLimitPolicy(options);
                    });
                });
                webHost.Configure(app =>
                {
                    // The order Umbraco's pipeline runs its filters in: PreRouting, routing,
                    // PostRouting (the webhook filter's UseRateLimiter, then this endpoint's
                    // authentication), then Endpoints.
                    McpEndpointWiring.UsePreRouting(app);
                    app.UseRouting();
                    app.UseRateLimiter();
                    McpEndpointWiring.UsePostRouting(app);

                    app.UseEndpoints(endpoints =>
                    {
                        McpEndpointWiring.MapEndpoints(endpoints);

                        // An unrelated route outside the MCP path prefix, mapped through the same
                        // pipeline. If McpAuthenticationMiddleware were ever wired unscoped again,
                        // this route would start being refused too.
                        endpoints.MapGet("/some-other-route", () => Results.Ok("ok"));
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();

        // The MCP Streamable HTTP transport requires callers to advertise support for both
        // response shapes on every POST; without this the endpoint replies 406 Not Acceptable
        // regardless of routing/auth wiring. Real MCP clients send this too — it's a protocol
        // requirement, not something particular to this test host.
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    }

    private Task<HttpResponseMessage> PostAsync(Guid automationId, object body)
        => _client.PostAsJsonAsync($"{Constants.McpApi.PathPrefix}/{automationId}", body);

    private Task<HttpResponseMessage> InitializeSessionAsync()
        => PostAsync(_automationId, new
        {
            jsonrpc = "2.0",
            id = 0,
            method = "initialize",
            @params = new
            {
                protocolVersion = "2024-11-05",
                capabilities = new { },
                clientInfo = new { name = "test-client", version = "1.0.0" },
            },
        });

    private Task<HttpResponseMessage> ListToolsAsync(Guid automationId)
        => PostAsync(automationId, new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
        });

    [Fact]
    public async Task ListTools_ReturnsTheAutomationsOwnTool()
    {
        (await InitializeSessionAsync()).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await ListToolsAsync(_automationId);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var document = ParseJsonRpcBody(body);
        var tools = document.RootElement.GetProperty("result").GetProperty("tools");
        tools.GetArrayLength().ShouldBe(1);
        tools[0].GetProperty("name").GetString().ShouldBe("Echo");
    }

    [Fact]
    public async Task CallTool_ExecutesAndReturnsResult()
    {
        (await InitializeSessionAsync()).StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await PostAsync(_automationId, new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "Echo",
                arguments = new { message = "hello" },
            },
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        using var document = ParseJsonRpcBody(body);
        var result = document.RootElement.GetProperty("result");
        (!result.TryGetProperty("isError", out var isError) || isError.GetBoolean() == false).ShouldBeTrue();
        var content = result.GetProperty("content");
        content.GetArrayLength().ShouldBeGreaterThan(0);
        content[0].GetProperty("text").GetString().ShouldBe("Automation completed.");
    }

    [Fact]
    public async Task UnknownAutomation_Returns401LikeAWrongSecret()
    {
        var response = await ListToolsAsync(Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MoreCallsThanTheRateLimit_AreRefusedWith429()
    {
        for (var i = 0; i < RateLimitPerMinute; i++)
        {
            (await ListToolsAsync(_automationId)).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests);
        }

        var response = await ListToolsAsync(_automationId);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    /// <summary>
    /// Sends a declared Content-Length, which the middleware itself rejects. A chunked body with no
    /// declared length is capped by the server through <c>IHttpMaxRequestBodySizeFeature</c>, which
    /// Kestrel and IIS provide but <see cref="TestServer"/> doesn't, so that path isn't covered here.
    /// </summary>
    [Fact]
    public async Task DeclaredBodyOverTheSizeLimit_IsRefusedWith413()
    {
        var json = JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = "Echo",
                arguments = new { message = new string('x', (int)MaxPayloadBytes * 2) },
            },
        });
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _client.PostAsync($"{Constants.McpApi.PathPrefix}/{_automationId}", content);

        response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
    }

    /// <summary>
    /// Regression test for the site-wide outage found via manual demo-site testing: wiring
    /// <see cref="McpAuthenticationMiddleware"/> in unscoped made it refuse every request in the
    /// app, not just MCP requests, because it refuses whenever "automationId" isn't present in
    /// route values — true for every non-MCP route. The host uses the production
    /// <see cref="McpEndpointWiring.UsePostRouting"/>, so a regression back to the unscoped form
    /// fails this test rather than only surfacing against a real running app.
    /// </summary>
    [Fact]
    public async Task RegularRoute_IsNotInterceptedByMcpAuthMiddleware()
    {
        var response = await _client.GetAsync("/some-other-route");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// The MCP Streamable HTTP transport may reply with a plain JSON body or with an SSE-framed
    /// body (lines like <c>event: message</c> / <c>data: {...}</c>), depending on negotiation.
    /// Tests assert on the JSON-RPC payload either way, so this unwraps the SSE framing when present.
    /// </summary>
    private static JsonDocument ParseJsonRpcBody(string body)
    {
        var dataLine = body
            .Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .FirstOrDefault(line => line.StartsWith("data:", StringComparison.Ordinal));

        var json = dataLine is not null ? dataLine["data:".Length..].Trim() : body;
        return JsonDocument.Parse(json);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }
}
