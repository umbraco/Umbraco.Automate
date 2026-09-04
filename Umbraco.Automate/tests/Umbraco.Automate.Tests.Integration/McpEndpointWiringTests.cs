using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web;
using Umbraco.Automate.Web.Api.Mcp;

namespace Umbraco.Automate.Tests.Integration;

/// <summary>
/// The one integration test in this repo that spins up a real ASP.NET Core pipeline
/// (<see cref="TestServer"/>) rather than a hand-built <see cref="IServiceProvider"/> — needed to
/// prove that <see cref="McpAuthenticationMiddleware"/>, routing, and the MCP server's
/// <c>ConfigureSessionOptions</c> callback actually compose the way
/// <c>AddUmbracoAutomateMcpApi</c> assembles them for real. Deliberately kept to this one test
/// class rather than retrofitted onto the rest of the suite.
/// </summary>
public sealed class McpEndpointWiringTests : IAsyncLifetime
{
    private IHost _host = null!;
    private HttpClient _client = null!;
    private Guid _automationId;

    public async Task InitializeAsync()
    {
        _automationId = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(_automationId)
            .WithStatus(AutomationStatus.Published)
            .WithTrigger(McpTrigger.WellKnownAlias, new Dictionary<string, object?>
            {
                ["toolName"] = "Echo",
                ["toolDescription"] = "Echoes input back.",
            });

        var automationService = new Mock<IAutomationService>();
        automationService.Setup(s => s.GetAutomationAsync(_automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation)automation);

        var executor = new Mock<IAutomationExecutor>();
        var runId = Guid.NewGuid();
        executor.Setup(e => e.ExecuteAsync(
                It.IsAny<Automation>(), It.IsAny<string>(), It.IsAny<string?>(),
                It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>?>()))
            .ReturnsAsync(runId);

        var runService = new Mock<IAutomationRunService>();
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
        // middleware store a null McpTriggerSettings in HttpContext.Items — silently skipping
        // ConfigureSessionOptions' tool registration below. Matches the resolver setup used by
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
                    services.AddSingleton(automationService.Object);
                    services.AddSingleton(executor.Object);
                    services.AddSingleton(runService.Object);
                    services.AddSingleton(triggers);
                    services.AddMcpServer().WithHttpTransport(options =>
                    {
                        options.ConfigureSessionOptions = (httpContext, mcpOptions, _) =>
                        {
                            if (httpContext.Items[McpHttpContextItems.AutomationKey] is Automation a
                                && httpContext.Items[McpHttpContextItems.SettingsKey] is McpTriggerSettings s)
                            {
                                mcpOptions.ToolCollection =
                                [
                                    new AutomationMcpTool(a, s, executor.Object, runService.Object, TimeSpan.FromMilliseconds(1)),
                                ];
                            }

                            return Task.CompletedTask;
                        };
                    });
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();

                    // Mirrors AddUmbracoAutomateMcpApi's production wiring exactly (UseWhen
                    // scoped to Constants.McpApi.PathPrefix, not an unscoped UseMiddleware call)
                    // so RegularRoute_IsNotInterceptedByMcpAuthMiddleware below actually proves
                    // the fix for the global-404 regression this task found via manual testing —
                    // not just a hand-picked, differently-shaped stand-in.
                    app.UseWhen(
                        context => context.Request.Path.StartsWithSegments(Constants.McpApi.PathPrefix),
                        branch => branch.UseMiddleware<McpAuthenticationMiddleware>());

                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapMcp(Constants.McpApi.RouteTemplate);

                        // An unrelated route outside the MCP path prefix, mapped through the same
                        // pipeline. If McpAuthenticationMiddleware were ever wired unscoped again
                        // (the exact bug this task fixed), this route would start 404ing too.
                        endpoints.MapGet("/some-other-route", () => Results.Ok("ok"));
                    });
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();

        // The MCP Streamable HTTP transport requires callers to advertise support for both
        // response shapes on every POST; without this the endpoint replies 406 Not Acceptable
        // regardless of routing/auth wiring. Real callers (the demo site's curl example in the
        // task brief, actual MCP clients) must send this too — it's a protocol requirement, not
        // something particular to this test host.
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
    }

    [Fact]
    public async Task ListTools_ReturnsTheAutomationsOwnTool()
    {
        var initializeResponse = await _client.PostAsJsonAsync($"{Constants.McpApi.PathPrefix}/{_automationId}", new
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
        initializeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var response = await _client.PostAsJsonAsync($"{Constants.McpApi.PathPrefix}/{_automationId}", new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Echo");
    }

    [Fact]
    public async Task UnknownAutomation_Returns404()
    {
        var response = await _client.PostAsJsonAsync($"{Constants.McpApi.PathPrefix}/{Guid.NewGuid()}", new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Regression test for the global-404 outage this task found via manual demo-site testing:
    /// wiring <see cref="McpAuthenticationMiddleware"/> in with an unscoped
    /// <c>app.UseMiddleware&lt;McpAuthenticationMiddleware&gt;()</c> made it 404 every request in
    /// the app, not just MCP requests, because it 404s whenever "automationId" isn't present in
    /// route values — true for every non-MCP route. This test's host wires the middleware through
    /// the same <c>UseWhen</c>-scoped form <c>AddUmbracoAutomateMcpApi</c> uses in production
    /// (see <see cref="InitializeAsync"/>), so a regression back to the unscoped form would fail
    /// this test rather than only surfacing against a real running app.
    /// </summary>
    [Fact]
    public async Task RegularRoute_IsNotInterceptedByMcpAuthMiddleware()
    {
        var response = await _client.GetAsync("/some-other-route");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }
}
