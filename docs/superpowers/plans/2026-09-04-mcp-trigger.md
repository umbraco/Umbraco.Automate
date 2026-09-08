# MCP Trigger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new built-in trigger, `McpTrigger`, so a published automation can act as its own MCP (Model Context Protocol) server — one callable tool, addressed by the automation's own URL, that starts the run and waits for the real result.

**Architecture:** `McpTrigger`/`McpTriggerSettings`/`McpTriggerOutput` live in `Umbraco.Automate.Core` next to `WebhookTrigger`, auto-discovered by Umbraco's `TypeLoader` — no manual registration needed. The HTTP surface lives in `Umbraco.Automate.Web` using Microsoft's `ModelContextProtocol.AspNetCore` package: a `McpAuthenticationMiddleware` resolves the automation and checks the bearer secret before the request reaches the MCP handler, and a per-request `AutomationMcpTool` (one dynamically-built `McpServerTool` per automation) executes the run and polls `IAutomationRunService` for the result instead of listening for an in-process completion event, so it stays correct when Automate runs on more than one server.

**Tech Stack:** .NET 10, ASP.NET Core, `ModelContextProtocol.AspNetCore` (new dependency), WorkflowCore (via existing `IAutomationExecutor`), xUnit + Shouldly + Moq.

**Spec:** `docs/superpowers/specs/2026-09-04-mcp-trigger-design.md`

## Global Constraints

- No new Connection type, no workspace-level MCP concept — everything lives on the trigger's own settings (spec, "Goals").
- Route: `automate/mcp/{automationId}` (spec, "Endpoint & protocol").
- Transport: Streamable HTTP only, stateless — no SSE, no stdio (spec, "Endpoint & protocol").
- Wait for the result by polling `IAutomationRunService`, never the in-process `AutomationRunCompletedNotification` — Automate can run on more than one instance (spec, "Execution & wait semantics").
- `tools/list` on one automation's URL always returns exactly one tool (spec, "Non-goals" — no router/aggregation model).
- A blank `Secret` is a deliberate author opt-out of auth, not a default (spec, "Authentication").
- Default `TimeoutSeconds` is 30.

---

## Task 1: MCP trigger data model

**Files:**
- Create: `Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/McpToolInputFieldType.cs`
- Create: `Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/McpToolInputField.cs`
- Create: `Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/McpTriggerSettings.cs`
- Create: `Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/McpTriggerOutput.cs`
- Test: `Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Triggers/BuiltIn/McpTriggerSettingsTests.cs`

**Interfaces:**
- Produces: `McpToolInputFieldType { Text, Number, Boolean }`; `McpToolInputField { string Name, McpToolInputFieldType Type, string? Description, bool Required }`; `McpTriggerSettings { string ToolName, string ToolDescription, List<McpToolInputField> InputFields, string? Secret, int TimeoutSeconds = 30, string? TestArguments }`; `McpTriggerOutput { Dictionary<string, object?> Arguments }`. These are consumed by every later task.

- [ ] **Step 1: Write the failing test for `McpTriggerSettings` defaults**

```csharp
using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public sealed class McpTriggerSettingsTests
{
    [Fact]
    public void DefaultTimeoutSeconds_Is30()
    {
        new McpTriggerSettings().TimeoutSeconds.ShouldBe(30);
    }

    [Fact]
    public void DefaultInputFields_IsEmpty()
    {
        new McpTriggerSettings().InputFields.ShouldBeEmpty();
    }

    [Fact]
    public void DefaultSecret_IsNull()
    {
        new McpTriggerSettings().Secret.ShouldBeNull();
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpTriggerSettingsTests`
Expected: FAIL — `McpTriggerSettings` does not exist yet.

- [ ] **Step 3: Create the field type enum**

```csharp
namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// The JSON Schema type a declared <see cref="McpToolInputField"/> is exposed as to the AI agent.
/// </summary>
public enum McpToolInputFieldType
{
    Text,
    Number,
    Boolean,
}
```

- [ ] **Step 4: Create the input field POCO**

```csharp
namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// One argument the <see cref="McpTrigger"/>'s tool accepts. Becomes a property in the tool's
/// JSON input schema shown to the AI agent, and a key in <see cref="McpTriggerOutput.Arguments"/>.
/// </summary>
public sealed class McpToolInputField
{
    public string Name { get; set; } = string.Empty;

    public McpToolInputFieldType Type { get; set; } = McpToolInputFieldType.Text;

    public string? Description { get; set; }

    public bool Required { get; set; }
}
```

- [ ] **Step 5: Create `McpTriggerSettings`**

`TestArguments` plays the same role `TestRequestBody` plays on `WebhookTriggerSettings` — the stand-in payload "Run now" sends when there's no real MCP caller (see `Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/WebhookTriggerSettings.cs:30-47` for the precedent this mirrors).

```csharp
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Settings for the <see cref="McpTrigger"/>.
/// </summary>
public sealed class McpTriggerSettings
{
    [Field(
        Label = "Tool Name",
        Description = "The name the AI agent sees for this tool.")]
    public string ToolName { get; set; } = string.Empty;

    [Field(
        Label = "Tool Description",
        Description = "Tells the AI agent when to use this tool.",
        EditorUiAlias = "Umb.PropertyEditorUi.TextArea",
        SortOrder = 10)]
    public string ToolDescription { get; set; } = string.Empty;

    [Field(
        Label = "Input Fields",
        Description = "The arguments this tool accepts. Each becomes part of the tool's schema and is available to steps as Arguments[\"name\"].",
        EditorUiAlias = "UmbracoAutomate.PropertyEditorUi.McpInputFieldsBuilder",
        SortOrder = 20)]
    public List<McpToolInputField> InputFields { get; set; } = [];

    [Field(
        Label = "Secret",
        Description = "Bearer token the caller must send in the Authorization header. Leave blank to allow calls with no authentication.",
        IsSensitive = true,
        EditorUiAlias = "Umb.Automate.WebhookSecretField",
        SortOrder = 30)]
    public string? Secret { get; set; }

    [Field(
        Label = "Timeout (seconds)",
        Description = "How long to wait for the run to finish before telling the agent it's still running.",
        SortOrder = 40)]
    public int TimeoutSeconds { get; set; } = 30;

    [Field(
        Label = "Test arguments",
        Description = "The arguments to use when running this automation on demand, as a JSON object matching the input fields above.",
        EditorUiAlias = "Umb.PropertyEditorUi.CodeEditor",
        EditorConfig = """
            [
                { "alias": "language", "value": "json" },
                { "alias": "height", "value": 160 },
                { "alias": "wordWrap", "value": true }
            ]
            """,
        SortOrder = 100)]
    public string? TestArguments { get; set; }
}
```

- [ ] **Step 6: Create `McpTriggerOutput`**

```csharp
namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Output produced by the <see cref="McpTrigger"/> containing the AI agent's tool-call arguments.
/// </summary>
public sealed class McpTriggerOutput
{
    public Dictionary<string, object?> Arguments { get; init; } = [];
}
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpTriggerSettingsTests`
Expected: PASS

- [ ] **Step 8: Commit**

```bash
git add Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/McpToolInputFieldType.cs \
        Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/McpToolInputField.cs \
        Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/McpTriggerSettings.cs \
        Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/McpTriggerOutput.cs \
        Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Triggers/BuiltIn/McpTriggerSettingsTests.cs
git commit -m "feat(trigger): Add MCP trigger settings and output model"
```

---

## Task 2: `McpTrigger`

**Files:**
- Create: `Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/McpTrigger.cs`
- Test: `Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Triggers/BuiltIn/McpTriggerTests.cs`

**Interfaces:**
- Consumes: `McpTriggerSettings`, `McpTriggerOutput` (Task 1); `TriggerBase<TSettings,TOutput>`, `TriggerInfrastructure`, `ISupportsManualRun`, `ManualRunOutput` (existing).
- Produces: `McpTrigger.WellKnownAlias = "umbracoAutomate.mcp"`, consumed by Task 5's middleware (`triggers.GetByAlias<McpTrigger>(...)`) and by test builders in later tasks.

This mirrors `Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/WebhookTrigger.cs` exactly, including its `CreateManualRunOutput`/`ParseTestHeaders` pattern (lines 42-104), swapped to parse `TestArguments` instead of headers.

- [ ] **Step 1: Write the failing tests**

```csharp
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Triggers.BuiltIn;

public sealed class McpTriggerTests
{
    private readonly McpTrigger _trigger = new(
        new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()));

    [Fact]
    public void HasCorrectAlias()
    {
        _trigger.Alias.ShouldBe("umbracoAutomate.mcp");
    }

    [Fact]
    public void HasSettingsAndOutputTypes()
    {
        _trigger.SettingsType.ShouldBe(typeof(McpTriggerSettings));
        _trigger.OutputType.ShouldBe(typeof(McpTriggerOutput));
    }

    [Fact]
    public void CreateManualRunOutput_NoTestArguments_ReturnsEmptyArguments()
    {
        var settings = new McpTriggerSettings();

        var result = _trigger.CreateManualRunOutput(settings);

        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.ShouldContainKey("arguments");
    }

    [Fact]
    public void CreateManualRunOutput_ValidTestArguments_ReturnsThem()
    {
        var settings = new McpTriggerSettings { TestArguments = """{ "customerEmail": "a@b.com" }""" };

        var result = _trigger.CreateManualRunOutput(settings);

        result.Success.ShouldBeTrue();
        var arguments = result.Data!["arguments"] as Dictionary<string, object?>;
        arguments.ShouldNotBeNull();
        arguments!["customerEmail"].ShouldBe("a@b.com");
    }

    [Fact]
    public void CreateManualRunOutput_InvalidJson_ReturnsInvalid()
    {
        var settings = new McpTriggerSettings { TestArguments = "not json" };

        var result = _trigger.CreateManualRunOutput(settings);

        result.Success.ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpTriggerTests`
Expected: FAIL — `McpTrigger` does not exist yet.

- [ ] **Step 3: Implement `McpTrigger`**

```csharp
using System.Text.Json;
using Umbraco.Automate.Core.Dispatch;

namespace Umbraco.Automate.Core.Triggers.BuiltIn;

/// <summary>
/// Built-in trigger that makes an automation callable as an MCP tool over its own URL.
/// </summary>
[Trigger(WellKnownAlias, "MCP Tool",
    Description = "Fires when an AI agent calls this automation's MCP tool.",
    Group = "Core",
    Icon = "icon-plug")]
public sealed class McpTrigger : TriggerBase<McpTriggerSettings, McpTriggerOutput>, ISupportsManualRun
{
    public const string WellKnownAlias = "umbracoAutomate.mcp";

    public McpTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    /// <remarks>
    /// Parses the saved <see cref="McpTriggerSettings.TestArguments"/> the same way an incoming
    /// MCP tool call's arguments would be bound, so a step sees identical data whether it was
    /// exercised on demand or by a real agent. The bearer-secret check has no bearing here — the
    /// point is exercising the steps, not the endpoint's authentication.
    /// </remarks>
    public ManualRunOutput CreateManualRunOutput(object? settings)
    {
        var typedSettings = settings as McpTriggerSettings;

        var arguments = ParseTestArguments(typedSettings?.TestArguments);
        if (arguments is null)
        {
            return ManualRunOutput.Invalid(
                "The MCP trigger's test arguments are not a JSON object. Fix them in the trigger's settings.");
        }

        var output = new McpTriggerOutput { Arguments = arguments };

        return ManualRunOutput.From(
            JsonOptions.DeserializeToUnwrappedDictionary(JsonSerializer.Serialize(output, JsonOptions.Default)));
    }

    /// <summary>
    /// Parses the saved test arguments as a JSON object. Returns <c>null</c> when the text is
    /// present but isn't a JSON object, so the run is refused rather than started with arguments
    /// the author didn't mean. Blank text is treated as "no arguments".
    /// </summary>
    private static Dictionary<string, object?>? ParseTestArguments(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonOptions.DeserializeToUnwrappedDictionary(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpTriggerTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/BuiltIn/McpTrigger.cs \
        Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Triggers/BuiltIn/McpTriggerTests.cs
git commit -m "feat(trigger): Add McpTrigger built-in trigger"
```

**Suggested PR boundary:** Tasks 1-2 form one PR — pure `Umbraco.Automate.Core` additions, no new dependency, fully covered by unit tests. This is the base of the stack.

---

## Task 3: `ModelContextProtocol.AspNetCore` package, `McpOptions`, `Constants.McpApi`

**Files:**
- Modify: `Umbraco.Automate/Directory.Packages.props`
- Modify: `Umbraco.Automate/src/Umbraco.Automate.Web/Umbraco.Automate.Web.csproj`
- Modify: `Umbraco.Automate/src/Umbraco.Automate.Core/Configuration/AutomateOptions.cs`
- Modify: `Umbraco.Automate/src/Umbraco.Automate.Core/Configuration/UmbracoBuilderExtensions.Collections.cs`
- Modify: `Umbraco.Automate/src/Umbraco.Automate.Web/Constants.cs`
- Modify: `Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/TriggerInitiatorType.cs`
- Test: `Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Configuration/McpOptionsTests.cs`

**Interfaces:**
- Produces: `McpOptions { int RateLimitPerMinute = 60 }` bound to `Umbraco:Automate:Mcp`; `Constants.McpApi { ApiName = "automate-mcp", ApiTitle, RateLimitPolicy = "automate-mcp-rate-limit" }`; `TriggerInitiatorType.AiAgent = "ai-agent"`. Consumed by Tasks 6 and 7.

**Note on the initiator constant:** `AutomationRun.InitiatedBy`, `TriggerEventMessage.InitiatorType`, and `IAutomationExecutor.ExecuteAsync`'s doc comments already list `"ai-agent"` as a well-known value (see `Umbraco.Automate/src/Umbraco.Automate.Core/Execution/IAutomationExecutor.cs:14`), but `TriggerInitiatorType` (`Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/TriggerInitiatorType.cs`) never actually defines the constant for it. This task adds it. It's deliberately excluded from `IsInteractive` — an MCP call is an autonomous agent action, not the "deliberate human action" that constant exists to let bypass the circuit breaker.

- [ ] **Step 1: Write the failing test for `McpOptions` defaults**

```csharp
using Shouldly;
using Umbraco.Automate.Core.Configuration;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Configuration;

public sealed class McpOptionsTests
{
    [Fact]
    public void DefaultRateLimitPerMinute_Is60()
    {
        new McpOptions().RateLimitPerMinute.ShouldBe(60);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpOptionsTests`
Expected: FAIL — `McpOptions` does not exist yet.

- [ ] **Step 3: Add `McpOptions`**

Add to `Umbraco.Automate/src/Umbraco.Automate.Core/Configuration/AutomateOptions.cs`, immediately after the existing `WebhookOptions` class:

```csharp
/// <summary>
/// Configuration options for the MCP trigger endpoint.
/// Bound to <c>Umbraco:Automate:Mcp</c> in appsettings.json.
/// </summary>
public sealed class McpOptions
{
    /// <summary>
    /// Gets or sets the maximum number of MCP tool calls per automation per minute.
    /// Requests exceeding this limit are rejected with <c>429 Too Many Requests</c>.
    /// Default: 60.
    /// </summary>
    public int RateLimitPerMinute { get; set; } = 60;
}
```

- [ ] **Step 4: Bind `McpOptions`**

In `Umbraco.Automate/src/Umbraco.Automate.Core/Configuration/UmbracoBuilderExtensions.Collections.cs`, immediately after the existing `WebhookOptions` binding (around line 62-63):

```csharp
        builder.Services.Configure<McpOptions>(
            builder.Config.GetSection("Umbraco:Automate:Mcp"));
```

- [ ] **Step 5: Add `Constants.McpApi`**

In `Umbraco.Automate/src/Umbraco.Automate.Web/Constants.cs`, immediately after the existing `WebhookApi` class:

```csharp
    /// <summary>
    /// Constants for the Automate MCP API (public, secret-authenticated per automation).
    /// </summary>
    public static class McpApi
    {
        /// <summary>
        /// The API name used for Swagger doc and JSON options.
        /// </summary>
        public const string ApiName = "automate-mcp";

        /// <summary>
        /// The API title.
        /// </summary>
        public const string ApiTitle = "Umbraco Automate MCP API";

        /// <summary>
        /// The rate limiter policy name applied to MCP endpoints.
        /// </summary>
        public const string RateLimitPolicy = "automate-mcp-rate-limit";
    }
```

- [ ] **Step 6: Add the AI-agent initiator constant**

In `Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/TriggerInitiatorType.cs`, after `Replay`:

```csharp
    /// <summary>
    /// Triggered by an AI agent calling an automation's MCP tool. Deliberately not
    /// <see cref="IsInteractive"/> — an agent call is an autonomous action, not the kind of
    /// deliberate human action the circuit breaker lets bypass an auto-disabled automation.
    /// </summary>
    public const string AiAgent = "ai-agent";
```

- [ ] **Step 7: Run test to verify it passes**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpOptionsTests`
Expected: PASS

- [ ] **Step 8: Add the `ModelContextProtocol.AspNetCore` package reference**

Run from the repo root:

```bash
dotnet add Umbraco.Automate/src/Umbraco.Automate.Web/Umbraco.Automate.Web.csproj package ModelContextProtocol.AspNetCore
```

Central package management (`ManagePackageVersionsCentrally=true`, set in `Umbraco.Automate/Directory.Packages.props`) means this writes a `<PackageVersion Include="ModelContextProtocol.AspNetCore" Version="X.Y.Z" />` line into `Umbraco.Automate/Directory.Packages.props` (using whatever the latest stable version resolves to — do not hand-pick a version number) and a bare `<PackageReference Include="ModelContextProtocol.AspNetCore" />` into `Umbraco.Automate.Web.csproj`'s existing `<ItemGroup>`, alongside `Microsoft.AspNetCore.OpenApi`. Open both files afterward and confirm those two lines landed exactly that way — if `dotnet add` wrote an inline version onto the `.csproj` reference instead of the bare form, move the version to `Directory.Packages.props` and strip it from the `.csproj` line by hand, matching every other reference in that file.

- [ ] **Step 9: Confirm the solution still builds**

Run: `dotnet build Umbraco.Automate/Umbraco.Automate.slnx`
Expected: Build succeeds.

- [ ] **Step 10: Commit**

```bash
git add Umbraco.Automate/Directory.Packages.props \
        Umbraco.Automate/src/Umbraco.Automate.Web/Umbraco.Automate.Web.csproj \
        Umbraco.Automate/src/Umbraco.Automate.Core/Configuration/AutomateOptions.cs \
        Umbraco.Automate/src/Umbraco.Automate.Core/Configuration/UmbracoBuilderExtensions.Collections.cs \
        Umbraco.Automate/src/Umbraco.Automate.Web/Constants.cs \
        Umbraco.Automate/src/Umbraco.Automate.Core/Triggers/TriggerInitiatorType.cs \
        Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Configuration/McpOptionsTests.cs
git commit -m "chore(deps): Add ModelContextProtocol.AspNetCore and MCP endpoint configuration"
```

---

## Task 4: Tool input schema and argument binding

**Files:**
- Create: `Umbraco.Automate/src/Umbraco.Automate.Web/Api/Mcp/McpToolSchemaBuilder.cs`
- Create: `Umbraco.Automate/src/Umbraco.Automate.Web/Api/Mcp/McpToolArgumentBinder.cs`
- Test: `Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Mcp/McpToolSchemaBuilderTests.cs`
- Test: `Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Mcp/McpToolArgumentBinderTests.cs`

**Interfaces:**
- Consumes: `McpToolInputField`, `McpToolInputFieldType` (Task 1).
- Produces: `McpToolSchemaBuilder.BuildInputSchema(IReadOnlyList<McpToolInputField> fields) : JsonElement`; `McpToolArgumentBinder.TryBind(IReadOnlyList<McpToolInputField> fields, IDictionary<string, JsonElement>? arguments, out Dictionary<string, object?> bound, out string? error) : bool`. Both consumed by Task 6.

This is pure logic with no MCP-SDK server plumbing and no I/O — fully unit-testable in isolation before Task 6 wires it into a real `McpServerTool`.

- [ ] **Step 1: Write the failing schema-builder tests**

```csharp
using System.Text.Json;
using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Web.Api.Mcp;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Mcp;

public sealed class McpToolSchemaBuilderTests
{
    [Fact]
    public void BuildInputSchema_NoFields_ReturnsEmptyObjectSchema()
    {
        var schema = McpToolSchemaBuilder.BuildInputSchema([]);

        schema.GetProperty("type").GetString().ShouldBe("object");
        schema.GetProperty("properties").EnumerateObject().ShouldBeEmpty();
    }

    [Fact]
    public void BuildInputSchema_MapsFieldTypesAndRequired()
    {
        var fields = new List<McpToolInputField>
        {
            new() { Name = "customerEmail", Type = McpToolInputFieldType.Text, Required = true, Description = "Who to email" },
            new() { Name = "amount", Type = McpToolInputFieldType.Number, Required = false },
            new() { Name = "urgent", Type = McpToolInputFieldType.Boolean, Required = false },
        };

        var schema = McpToolSchemaBuilder.BuildInputSchema(fields);

        var properties = schema.GetProperty("properties");
        properties.GetProperty("customerEmail").GetProperty("type").GetString().ShouldBe("string");
        properties.GetProperty("customerEmail").GetProperty("description").GetString().ShouldBe("Who to email");
        properties.GetProperty("amount").GetProperty("type").GetString().ShouldBe("number");
        properties.GetProperty("urgent").GetProperty("type").GetString().ShouldBe("boolean");

        var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToList();
        required.ShouldBe(["customerEmail"]);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpToolSchemaBuilderTests`
Expected: FAIL — `McpToolSchemaBuilder` does not exist yet.

- [ ] **Step 3: Implement `McpToolSchemaBuilder`**

```csharp
using System.Text.Json;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// Builds the JSON Schema an <see cref="McpTrigger"/>'s declared input fields are advertised as
/// to an AI agent, matching the shape <see cref="ModelContextProtocol.Protocol.Tool.InputSchema"/> requires.
/// </summary>
internal static class McpToolSchemaBuilder
{
    public static JsonElement BuildInputSchema(IReadOnlyList<McpToolInputField> fields)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var field in fields)
        {
            var property = new Dictionary<string, object?>
            {
                ["type"] = ToJsonSchemaType(field.Type),
            };

            if (!string.IsNullOrEmpty(field.Description))
            {
                property["description"] = field.Description;
            }

            properties[field.Name] = property;

            if (field.Required)
            {
                required.Add(field.Name);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required,
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    private static string ToJsonSchemaType(McpToolInputFieldType type) => type switch
    {
        McpToolInputFieldType.Text => "string",
        McpToolInputFieldType.Number => "number",
        McpToolInputFieldType.Boolean => "boolean",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown MCP input field type."),
    };
}
```

- [ ] **Step 4: Run schema-builder tests to verify they pass**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpToolSchemaBuilderTests`
Expected: PASS

- [ ] **Step 5: Write the failing argument-binder tests**

```csharp
using System.Text.Json;
using Shouldly;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Web.Api.Mcp;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Mcp;

public sealed class McpToolArgumentBinderTests
{
    private static readonly List<McpToolInputField> Fields =
    [
        new() { Name = "customerEmail", Type = McpToolInputFieldType.Text, Required = true },
        new() { Name = "amount", Type = McpToolInputFieldType.Number, Required = false },
    ];

    private static IDictionary<string, JsonElement> Args(string json)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public void TryBind_AllPresentAndValid_ReturnsTrue()
    {
        var ok = McpToolArgumentBinder.TryBind(
            Fields, Args("""{"customerEmail":"a@b.com","amount":5}"""), out var bound, out var error);

        ok.ShouldBeTrue();
        error.ShouldBeNull();
        bound["customerEmail"].ShouldBe("a@b.com");
        bound["amount"].ShouldBe(5d);
    }

    [Fact]
    public void TryBind_MissingRequiredField_ReturnsFalse()
    {
        var ok = McpToolArgumentBinder.TryBind(Fields, Args("{}"), out _, out var error);

        ok.ShouldBeFalse();
        error.ShouldContain("customerEmail");
    }

    [Fact]
    public void TryBind_WrongType_ReturnsFalse()
    {
        var ok = McpToolArgumentBinder.TryBind(
            Fields, Args("""{"customerEmail":"a@b.com","amount":"not a number"}"""), out _, out var error);

        ok.ShouldBeFalse();
        error.ShouldContain("amount");
    }

    [Fact]
    public void TryBind_NullArguments_TreatsAsEmpty()
    {
        var ok = McpToolArgumentBinder.TryBind(
            [new() { Name = "optional", Type = McpToolInputFieldType.Text, Required = false }],
            null, out var bound, out var error);

        ok.ShouldBeTrue();
        error.ShouldBeNull();
        bound.ShouldBeEmpty();
    }
}
```

- [ ] **Step 6: Run tests to verify they fail**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpToolArgumentBinderTests`
Expected: FAIL — `McpToolArgumentBinder` does not exist yet.

- [ ] **Step 7: Implement `McpToolArgumentBinder`**

```csharp
using System.Text.Json;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// Validates an incoming MCP tool call's arguments against an <see cref="McpTrigger"/>'s declared
/// <see cref="McpToolInputField"/>s and converts them to plain CLR values. Extra arguments not
/// declared on the trigger are ignored rather than rejected.
/// </summary>
internal static class McpToolArgumentBinder
{
    public static bool TryBind(
        IReadOnlyList<McpToolInputField> fields,
        IDictionary<string, JsonElement>? arguments,
        out Dictionary<string, object?> bound,
        out string? error)
    {
        bound = [];
        arguments ??= new Dictionary<string, JsonElement>();

        foreach (var field in fields)
        {
            if (!arguments.TryGetValue(field.Name, out var value))
            {
                if (field.Required)
                {
                    error = $"Missing required argument '{field.Name}'.";
                    return false;
                }

                continue;
            }

            if (!TryConvert(field, value, out var converted))
            {
                error = $"Argument '{field.Name}' must be a {field.Type.ToString().ToLowerInvariant()}.";
                return false;
            }

            bound[field.Name] = converted;
        }

        error = null;
        return true;
    }

    private static bool TryConvert(McpToolInputField field, JsonElement value, out object? converted)
    {
        switch (field.Type)
        {
            case McpToolInputFieldType.Text when value.ValueKind == JsonValueKind.String:
                converted = value.GetString();
                return true;

            case McpToolInputFieldType.Number when value.ValueKind == JsonValueKind.Number:
                converted = value.GetDouble();
                return true;

            case McpToolInputFieldType.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                converted = value.GetBoolean();
                return true;

            default:
                converted = null;
                return false;
        }
    }
}
```

- [ ] **Step 8: Run tests to verify they pass**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpToolArgumentBinderTests`
Expected: PASS

- [ ] **Step 9: Commit**

```bash
git add Umbraco.Automate/src/Umbraco.Automate.Web/Api/Mcp/McpToolSchemaBuilder.cs \
        Umbraco.Automate/src/Umbraco.Automate.Web/Api/Mcp/McpToolArgumentBinder.cs \
        Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Mcp/McpToolSchemaBuilderTests.cs \
        Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Mcp/McpToolArgumentBinderTests.cs
git commit -m "feat(api): Add MCP tool schema builder and argument binder"
```

**Suggested PR boundary:** Task 3 and Task 4 form one PR, stacked on Tasks 1-2 — the new dependency plus the pure logic that doesn't yet touch HTTP.

---

## Task 5: Authentication middleware

**Files:**
- Create: `Umbraco.Automate/src/Umbraco.Automate.Web/Api/Mcp/McpHttpContextItems.cs`
- Create: `Umbraco.Automate/src/Umbraco.Automate.Web/Api/Mcp/McpAuthenticationMiddleware.cs`
- Test: `Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Mcp/McpAuthenticationMiddlewareTests.cs`

**Interfaces:**
- Consumes: `McpTrigger`, `McpTriggerSettings` (Task 1-2); `IAutomationService.GetAutomationAsync`, `TriggerCollection.GetByAlias<T>`, `Automation.Status`, `Automation.Trigger` (existing).
- Produces: `McpHttpContextItems.AutomationKey`, `McpHttpContextItems.SettingsKey` (the `HttpContext.Items` keys the middleware populates), consumed by Task 6/7's `ConfigureSessionOptions`.

This runs as ASP.NET Core middleware *before* the MCP handler `MapMcp` installs, mirroring the auth checks `WebhookEndpointController` does inline (see `Umbraco.Automate/src/Umbraco.Automate.Web/Api/Webhook/Controllers/WebhookEndpointController.cs:80-107`), but as middleware rather than a controller action, because `MapMcp`'s handler is not a controller we write ourselves.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Mcp;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Mcp;

public sealed class McpAuthenticationMiddlewareTests
{
    private readonly Mock<IAutomationService> _automationService = new();
    private readonly TriggerCollection _triggers;
    private bool _nextCalled;

    public McpAuthenticationMiddlewareTests()
    {
        _triggers = new TriggerCollection(() => [new McpTrigger(new TriggerInfrastructure(Mock.Of<IEditableModelResolver>()))]);
    }

    private (McpAuthenticationMiddleware Middleware, DefaultHttpContext Context) Build(Guid automationId)
    {
        var middleware = new McpAuthenticationMiddleware(_ =>
        {
            _nextCalled = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.RouteValues = new RouteValueDictionary { ["automationId"] = automationId.ToString() };

        return (middleware, context);
    }

    [Fact]
    public async Task InvokeAsync_AutomationNotFound_Returns404AndDoesNotCallNext()
    {
        var automationId = Guid.NewGuid();
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation?)null);
        var (middleware, context) = Build(automationId);

        await middleware.InvokeAsync(context, _automationService.Object, _triggers);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        _nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_NoSecretConfigured_CallsNext()
    {
        var automationId = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(automationId)
            .WithTrigger(McpTrigger.WellKnownAlias, new Dictionary<string, object?> { ["toolName"] = "Do Thing" });
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation)automation);
        var (middleware, context) = Build(automationId);

        await middleware.InvokeAsync(context, _automationService.Object, _triggers);

        _nextCalled.ShouldBeTrue();
        context.Items[McpHttpContextItems.AutomationKey].ShouldNotBeNull();
    }

    [Fact]
    public async Task InvokeAsync_SecretConfigured_WrongBearerToken_Returns401()
    {
        var automationId = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(automationId)
            .WithTrigger(McpTrigger.WellKnownAlias, new Dictionary<string, object?>
            {
                ["toolName"] = "Do Thing",
                ["secret"] = "correct-secret",
            });
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation)automation);
        var (middleware, context) = Build(automationId);
        context.Request.Headers.Authorization = "Bearer wrong-secret";

        await middleware.InvokeAsync(context, _automationService.Object, _triggers);

        context.Response.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        _nextCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_SecretConfigured_CorrectBearerToken_CallsNext()
    {
        var automationId = Guid.NewGuid();
        var automation = new AutomationBuilder()
            .WithId(automationId)
            .WithTrigger(McpTrigger.WellKnownAlias, new Dictionary<string, object?>
            {
                ["toolName"] = "Do Thing",
                ["secret"] = "correct-secret",
            });
        _automationService.Setup(s => s.GetAutomationAsync(automationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Automation)automation);
        var (middleware, context) = Build(automationId);
        context.Request.Headers.Authorization = "Bearer correct-secret";

        await middleware.InvokeAsync(context, _automationService.Object, _triggers);

        _nextCalled.ShouldBeTrue();
    }
}
```

`AutomationBuilder.WithTrigger(string, Dictionary<string,object?>)` already exists (see `Umbraco.Automate/src/Umbraco.Automate.Testing/Builders/AutomationBuilder.cs`); the `AutomationBuilder → Automation` conversion is an implicit operator, also already present. Note `WithId` sets `AutomationBuilder`'s default `Status` to `AutomationStatus.Published`, matching every other builder-based test in this repo (verify against `Umbraco.Automate.Testing/Builders/AutomationBuilder.cs`'s default — if it defaults to `Draft` instead, call `.Build()` and confirm, or chain nothing extra since `Published` is the documented default per the builder's own `WithStatus` remarks; if it turns out to default to `Draft`, the middleware's automations-must-be-published check would make these tests fail with 409 instead of the codes asserted above — fix by adding `.WithStatus(AutomationStatus.Published)` to each builder chain, not by loosening the middleware).

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpAuthenticationMiddlewareTests`
Expected: FAIL — `McpAuthenticationMiddleware` does not exist yet.

- [ ] **Step 3: Create `McpHttpContextItems`**

```csharp
namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/> keys the MCP authentication
/// middleware populates for the MCP server's per-request session configuration to read,
/// so the automation and its settings are resolved from the database exactly once per request.
/// </summary>
internal static class McpHttpContextItems
{
    public const string AutomationKey = "Umbraco.Automate.Mcp.Automation";

    public const string SettingsKey = "Umbraco.Automate.Mcp.Settings";
}
```

- [ ] **Step 4: Implement `McpAuthenticationMiddleware`**

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// Resolves the automation addressed by the MCP endpoint's <c>{automationId}</c> route value,
/// checks it's published with an <see cref="McpTrigger"/>, and checks the caller's bearer token
/// against the trigger's configured secret — all before the request reaches the MCP handler.
/// A blank secret is a deliberate author choice to allow unauthenticated calls.
/// </summary>
internal sealed class McpAuthenticationMiddleware
{
    private readonly RequestDelegate _next;

    public McpAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IAutomationService automationService, TriggerCollection triggers)
    {
        if (!context.Request.RouteValues.TryGetValue("automationId", out var raw)
            || raw is not string idText
            || !Guid.TryParse(idText, out var automationId))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var automation = await automationService.GetAutomationAsync(automationId, context.RequestAborted);
        if (automation is null || automation.Status != AutomationStatus.Published)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var trigger = automation.Trigger is not null
            ? triggers.GetByAlias<McpTrigger>(automation.Trigger.TriggerAlias)
            : null;
        if (trigger is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var settings = automation.Trigger?.Settings is { Count: > 0 } rawSettings
            ? trigger.ResolveSettings(rawSettings)
            : new McpTriggerSettings();

        if (!string.IsNullOrEmpty(settings?.Secret))
        {
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (authHeader is null || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var provided = authHeader["Bearer ".Length..];
            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(settings.Secret),
                    Encoding.UTF8.GetBytes(provided)))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        context.Items[McpHttpContextItems.AutomationKey] = automation;
        context.Items[McpHttpContextItems.SettingsKey] = settings;

        await _next(context);
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpAuthenticationMiddlewareTests`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add Umbraco.Automate/src/Umbraco.Automate.Web/Api/Mcp/McpHttpContextItems.cs \
        Umbraco.Automate/src/Umbraco.Automate.Web/Api/Mcp/McpAuthenticationMiddleware.cs \
        Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Mcp/McpAuthenticationMiddlewareTests.cs
git commit -m "feat(api): Add MCP endpoint authentication middleware"
```

---

## Task 6: `AutomationMcpTool` — execute and wait for the result

**Files:**
- Create: `Umbraco.Automate/src/Umbraco.Automate.Web/Api/Mcp/AutomationMcpTool.cs`
- Test: `Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Mcp/AutomationMcpToolTests.cs`

**Interfaces:**
- Consumes: `McpToolSchemaBuilder.BuildInputSchema`, `McpToolArgumentBinder.TryBind` (Task 4); `IAutomationExecutor.ExecuteAsync`, `IAutomationRunService.GetRunAsync`, `AutomationRun`, `AutomationRunStatus`, `StepRun`, `StepRunStatus`, `TriggerInitiatorType.AiAgent` (existing/Task 3); `JsonOptions.DeserializeToUnwrappedDictionary` (existing, internal — reachable because `Umbraco.Automate.Core` declares `InternalsVisibleTo` for `Umbraco.Automate.Web`).
- Produces: `AutomationMcpTool` (an `McpServerTool` subclass), consumed by Task 7's `ConfigureSessionOptions`.

`McpServerTool` is abstract with exactly two members to implement: `Tool ProtocolTool { get; }` and `ValueTask<CallToolResult> InvokeAsync(RequestContext<CallToolRequestParams> request, CancellationToken cancellationToken = default)` (verified against `ModelContextProtocol.Core/Server/McpServerTool.cs` in the `modelcontextprotocol/csharp-sdk` repo). `CallToolRequestParams.Arguments` is `IDictionary<string, JsonElement>?`; `CallToolResult` has `IList<ContentBlock> Content` and `bool? IsError` (verified against `ModelContextProtocol.Core/Protocol/CallToolRequestParams.cs` and `CallToolResult.cs` in the same repo).

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using Moq;
using Shouldly;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Mcp;
using Xunit;

namespace Umbraco.Automate.Tests.Unit.Mcp;

public sealed class AutomationMcpToolTests
{
    private readonly Mock<IAutomationExecutor> _executor = new();
    private readonly Mock<IAutomationRunService> _runService = new();

    private AutomationMcpTool BuildTool(McpTriggerSettings settings)
    {
        var automation = new AutomationBuilder().WithTrigger(McpTrigger.WellKnownAlias);
        return new AutomationMcpTool(
            automation, settings, _executor.Object, _runService.Object, pollInterval: TimeSpan.FromMilliseconds(1));
    }

    private static RequestContext<CallToolRequestParams> Request(string json)
        => new(server: null!)
        {
            Params = new CallToolRequestParams
            {
                Name = "tool",
                Arguments = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json),
            },
        };

    [Fact]
    public async Task InvokeAsync_MissingRequiredArgument_ReturnsErrorWithoutExecuting()
    {
        var settings = new McpTriggerSettings
        {
            InputFields = [new() { Name = "email", Type = McpToolInputFieldType.Text, Required = true }],
        };
        var tool = BuildTool(settings);

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(true);
        _executor.Verify(
            e => e.ExecuteAsync(It.IsAny<Umbraco.Automate.Core.Automations.Automation>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), It.IsAny<IReadOnlyList<Guid>?>()),
            Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_RunCompletesBeforeTimeout_ReturnsSuccessResult()
    {
        var runId = Guid.NewGuid();
        _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<Umbraco.Automate.Core.Automations.Automation>(), TriggerInitiatorType.AiAgent, null, It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(runId);
        _runService
            .Setup(s => s.GetRunAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = TriggerInitiatorType.AiAgent,
                Status = AutomationRunStatus.Completed,
                StepRuns = [new StepRun
                {
                    RunId = runId,
                    StepId = Guid.NewGuid(),
                    ActionAlias = "umbracoAutomate.logMessage",
                    Status = StepRunStatus.Completed,
                    OutputData = """{"message":"done"}""",
                }],
            });
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(false);
        var text = result.Content.OfType<TextContentBlock>().Single().Text;
        text.ShouldContain("done");
    }

    [Fact]
    public async Task InvokeAsync_RunFails_ReturnsErrorWithRunMessage()
    {
        var runId = Guid.NewGuid();
        _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<Umbraco.Automate.Core.Automations.Automation>(), TriggerInitiatorType.AiAgent, null, It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(runId);
        _runService
            .Setup(s => s.GetRunAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = TriggerInitiatorType.AiAgent,
                Status = AutomationRunStatus.Failed,
                Error = "Step 2 blew up",
            });
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 5 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(true);
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldBe("Step 2 blew up");
    }

    [Fact]
    public async Task InvokeAsync_RunStillRunningAtTimeout_ReturnsNonErrorStillRunningResult()
    {
        var runId = Guid.NewGuid();
        _executor
            .Setup(e => e.ExecuteAsync(It.IsAny<Umbraco.Automate.Core.Automations.Automation>(), TriggerInitiatorType.AiAgent, null, It.IsAny<Dictionary<string, object?>?>(), It.IsAny<CancellationToken>(), null))
            .ReturnsAsync(runId);
        _runService
            .Setup(s => s.GetRunAsync(runId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AutomationRun
            {
                AutomationId = Guid.NewGuid(),
                AutomationVersion = 1,
                WorkspaceId = Guid.NewGuid(),
                ServiceAccountKey = Guid.NewGuid(),
                InitiatedBy = TriggerInitiatorType.AiAgent,
                Status = AutomationRunStatus.Running,
            });
        var tool = BuildTool(new McpTriggerSettings { TimeoutSeconds = 0 });

        var result = await tool.InvokeAsync(Request("{}"));

        result.IsError.ShouldBe(false);
        result.Content.OfType<TextContentBlock>().Single().Text.ShouldContain(runId.ToString());
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter AutomationMcpToolTests`
Expected: FAIL — `AutomationMcpTool` does not exist yet.

- [ ] **Step 3: Implement `AutomationMcpTool`**

```csharp
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Dispatch;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;

namespace Umbraco.Automate.Web.Api.Mcp;

/// <summary>
/// The single MCP tool an automation's <see cref="McpTrigger"/> exposes at its own URL.
/// Executes the automation and polls <see cref="IAutomationRunService"/> for the result rather
/// than the in-process completion notification, so this stays correct when Automate runs on
/// more than one instance — the run may finalize on an instance other than the one holding this
/// request open.
/// </summary>
internal sealed class AutomationMcpTool : McpServerTool
{
    private readonly Automation _automation;
    private readonly McpTriggerSettings _settings;
    private readonly IAutomationExecutor _executor;
    private readonly IAutomationRunService _runService;
    private readonly TimeSpan _pollInterval;

    public AutomationMcpTool(
        Automation automation,
        McpTriggerSettings settings,
        IAutomationExecutor executor,
        IAutomationRunService runService,
        TimeSpan? pollInterval = null)
    {
        _automation = automation;
        _settings = settings;
        _executor = executor;
        _runService = runService;
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(250);

        ProtocolTool = new Tool
        {
            Name = settings.ToolName,
            Description = settings.ToolDescription,
            InputSchema = McpToolSchemaBuilder.BuildInputSchema(settings.InputFields),
        };
    }

    public override Tool ProtocolTool { get; }

    public override async ValueTask<CallToolResult> InvokeAsync(
        RequestContext<CallToolRequestParams> request,
        CancellationToken cancellationToken = default)
    {
        if (!McpToolArgumentBinder.TryBind(_settings.InputFields, request.Params?.Arguments, out var arguments, out var bindError))
        {
            return ErrorResult(bindError!);
        }

        var output = new McpTriggerOutput { Arguments = arguments };
        var triggerOutputData = JsonOptions.DeserializeToUnwrappedDictionary(
            JsonSerializer.Serialize(output, JsonOptions.Default));

        var runId = await _executor.ExecuteAsync(
            _automation,
            TriggerInitiatorType.AiAgent,
            initiatorId: null,
            triggerOutputData,
            cancellationToken);

        var deadline = DateTime.UtcNow.AddSeconds(_settings.TimeoutSeconds);
        AutomationRun? run;
        while (true)
        {
            run = await _runService.GetRunAsync(runId, cancellationToken);
            if (run is not null && IsTerminal(run.Status))
            {
                break;
            }

            if (DateTime.UtcNow >= deadline)
            {
                break;
            }

            await Task.Delay(_pollInterval, cancellationToken);
        }

        if (run is null || !IsTerminal(run.Status))
        {
            return new CallToolResult
            {
                IsError = false,
                Content = [new TextContentBlock
                {
                    Text = $"Still running after {_settings.TimeoutSeconds}s. Run ID: {runId}.",
                }],
            };
        }

        if (run.Status is AutomationRunStatus.Failed or AutomationRunStatus.Rejected)
        {
            return ErrorResult(run.Error ?? $"Run ended as {run.Status}.");
        }

        return new CallToolResult
        {
            IsError = false,
            Content = [new TextContentBlock { Text = BuildResultText(run) }],
        };
    }

    private static bool IsTerminal(AutomationRunStatus status)
        => status is AutomationRunStatus.Completed or AutomationRunStatus.Failed
            or AutomationRunStatus.Cancelled or AutomationRunStatus.Rejected;

    private static string BuildResultText(AutomationRun run)
    {
        var lastOutput = run.StepRuns
            .LastOrDefault(sr => sr.Status == Runs.StepRunStatus.Completed && sr.OutputData is not null);

        return lastOutput?.OutputData ?? "Automation completed.";
    }

    private static CallToolResult ErrorResult(string message) => new()
    {
        IsError = true,
        Content = [new TextContentBlock { Text = message }],
    };
}
```

Note: `StepRunStatus` lives in the same file as `AutomationRunStatus` (`Umbraco.Automate/src/Umbraco.Automate.Core/Runs/AutomationRunStatus.cs`), both in namespace `Umbraco.Automate.Core.Runs` — the `Runs.StepRunStatus` qualifier above is only needed if this file's usings create ambiguity; if `using Umbraco.Automate.Core.Runs;` alone resolves cleanly (it should, given the other `Runs` types used unqualified above), simplify `Runs.StepRunStatus.Completed` to plain `StepRunStatus.Completed`.

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter AutomationMcpToolTests`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add Umbraco.Automate/src/Umbraco.Automate.Web/Api/Mcp/AutomationMcpTool.cs \
        Umbraco.Automate/tests/Umbraco.Automate.Tests.Unit/Mcp/AutomationMcpToolTests.cs
git commit -m "feat(api): Add AutomationMcpTool execute-and-wait logic"
```

**Suggested PR boundary:** Tasks 5-6 form one PR, stacked on Task 3-4's PR — all the request-handling logic, each piece unit-tested in isolation, none of it wired to a real HTTP pipeline yet.

---

## Task 7: Wire the MCP server into the pipeline

**Files:**
- Modify: `Umbraco.Automate/src/Umbraco.Automate.Web/Configuration/UmbracoBuilderExtensions.cs`
- Test: `Umbraco.Automate/tests/Umbraco.Automate.Tests.Integration/McpEndpointWiringTests.cs`

**Interfaces:**
- Consumes: `McpAuthenticationMiddleware`, `McpHttpContextItems`, `AutomationMcpTool` (Task 5-6); `Constants.McpApi`, `McpOptions` (Task 3); the existing `AddUmbracoAutomateWebhookRateLimiting` pattern in this same file (lines 141-187, per the earlier fact-finding) as the template for `AddUmbracoAutomateMcpRateLimiting`.
- Produces: the live `automate/mcp/{automationId}` endpoint — nothing later in this plan depends on new symbols from this task; it's the integration point.

This is the one task that needs a real ASP.NET Core pipeline to prove out — none of the existing integration tests in this repo spin one up (they call handlers directly against a hand-built `ServiceProvider`, see `ManualTriggerLogMessageTests.cs`). This task adds a `Microsoft.AspNetCore.TestHost` (`TestServer`)-based test, new infrastructure for this repo, deliberately kept to this one test class rather than retrofitted everywhere.

- [ ] **Step 1: Write the failing integration test**

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Shouldly;
using Umbraco.Automate.Core.Automations;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Core.Execution;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Automate.Core.Triggers.BuiltIn;
using Umbraco.Automate.Testing.Builders;
using Umbraco.Automate.Web.Api.Mcp;
using Xunit;

namespace Umbraco.Automate.Tests.Integration;

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
            .WithTrigger(McpTrigger.WellKnownAlias, new Dictionary<string, object?> { ["toolName"] = "Echo" });

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

        var triggers = new TriggerCollection(() => [new McpTrigger(new TriggerInfrastructure(Mock.Of<Umbraco.Automate.Core.Settings.IEditableModelResolver>()))]);

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
                    app.UseMiddleware<McpAuthenticationMiddleware>();
                    app.UseEndpoints(endpoints => endpoints.MapMcp("mcp/{automationId}"));
                });
            })
            .StartAsync();

        _client = _host.GetTestClient();
    }

    [Fact]
    public async Task ListTools_ReturnsTheAutomationsOwnTool()
    {
        var response = await _client.PostAsJsonAsync($"mcp/{_automationId}", new
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
        var response = await _client.PostAsJsonAsync($"mcp/{Guid.NewGuid()}", new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/list",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _host.StopAsync();
        _host.Dispose();
    }
}
```

This test builds its own minimal host directly (rather than reusing `AddUmbracoAutomateMcpApi`) so it can substitute mocked services without a full Umbraco composition run. It exists to prove the *shape* of the wiring — `ConfigureSessionOptions` reading `HttpContext.Items`, `MapMcp` with a route parameter, the auth middleware running first — matches what `AddUmbracoAutomateMcpApi` (Step 3 below) will assemble for real inside Umbraco's own pipeline. `IEditableModelResolver`'s actual namespace is `Umbraco.Automate.Core.Settings` — confirm this import resolves; it's referenced fully-qualified above to avoid a stray `using` this file otherwise wouldn't need.

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpEndpointWiringTests`
Expected: FAIL — `McpAuthenticationMiddleware`'s constructor takes only `RequestDelegate` but middleware registered via `UseMiddleware<T>()` needs its other dependencies resolved per-invocation, which the current `InvokeAsync(HttpContext, IAutomationService, TriggerCollection)` signature already supports via ASP.NET Core's convention-based middleware DI — so this specific test should compile and mostly work already from Task 5-6's code. Expect it to fail here only because `AddUmbracoAutomateMcpApi` doesn't exist yet to compare against, or because a wiring detail (route pattern, `ConfigureSessionOptions` member name) doesn't match the installed package version. Resolve any compile error by checking the actual installed `HttpServerTransportOptions` API (see Step 3's note) before touching test code.

- [ ] **Step 3: Implement `AddUmbracoAutomateMcpApi`**

Add to `Umbraco.Automate/src/Umbraco.Automate.Web/Configuration/UmbracoBuilderExtensions.cs`, called from `AddUmbracoAutomateWeb` (after `builder.AddUmbracoAutomateWebhookApi();`):

```csharp
        builder.AddUmbracoAutomateMcpApi();
```

```csharp
    private static IUmbracoBuilder AddUmbracoAutomateMcpApi(this IUmbracoBuilder builder)
    {
        builder.Services.AddMcpServer().WithHttpTransport(options =>
        {
            // Check the installed ModelContextProtocol.AspNetCore version's HttpServerTransportOptions:
            // some versions expose a `SessionMode = HttpServerSessionMode.Stateless` enum, newer ones
            // a plain `Stateless = true` bool (defaulted to true as of SDK v2.0.0). Set whichever member
            // is actually present — stateless is required so ConfigureSessionOptions below runs on every
            // request rather than once per long-lived session, which is what makes per-automation tool
            // resolution correct without needing session affinity across Automate's own app instances.
            options.Stateless = true;

            options.ConfigureSessionOptions = (httpContext, mcpOptions, _) =>
            {
                if (httpContext.Items[McpHttpContextItems.AutomationKey] is Automation automation
                    && httpContext.Items[McpHttpContextItems.SettingsKey] is McpTriggerSettings settings)
                {
                    var executor = httpContext.RequestServices.GetRequiredService<IAutomationExecutor>();
                    var runService = httpContext.RequestServices.GetRequiredService<IAutomationRunService>();

                    mcpOptions.ToolCollection = [new AutomationMcpTool(automation, settings, executor, runService)];
                }

                return Task.CompletedTask;
            };
        });

        builder.AddUmbracoAutomateMcpRateLimiting();

        builder.Services.Configure<UmbracoPipelineOptions>(options =>
        {
            options.AddFilter(new UmbracoPipelineFilter("UmbracoAutomateMcp")
            {
                // Runs after routing (so {automationId} is in RouteValues) and before the MCP
                // handler mapped below.
                PostRouting = app => app.UseMiddleware<McpAuthenticationMiddleware>(),
                Endpoints = app => app.UseEndpoints(endpoints =>
                {
                    endpoints.MapMcp(Constants.McpApi.RouteTemplate)
                        .RequireRateLimiting(Constants.McpApi.RateLimitPolicy);
                }),
            });
        });

        return builder;
    }

    /// <summary>
    /// Registers the MCP rate limit policy only — the rate limiter middleware itself
    /// (<c>UseRateLimiter()</c>) is already installed globally by
    /// <see cref="AddUmbracoAutomateWebhookRateLimiting"/>. If that method's filter is ever
    /// removed, this policy will stop being enforced; the two are coupled on purpose to avoid
    /// registering the same middleware twice.
    /// </summary>
    private static IUmbracoBuilder AddUmbracoAutomateMcpRateLimiting(this IUmbracoBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy(Constants.McpApi.RateLimitPolicy, context =>
            {
                var mcpOptions = context.RequestServices
                    .GetRequiredService<IOptions<McpOptions>>().Value;

                var partitionKey = context.Request.RouteValues["automationId"]?.ToString() ?? "global";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: partitionKey,
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = mcpOptions.RateLimitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                    });
            });
        });

        return builder;
    }
```

Add the required `using Umbraco.Automate.Web.Api.Mcp;` and `using Umbraco.Automate.Core.Triggers.BuiltIn;` to this file's using block if not already present via a wildcard/shared import.

Add the route template constant next to the other `McpApi` constants added in Task 3 (`Umbraco.Automate/src/Umbraco.Automate.Web/Constants.cs`):

```csharp
        /// <summary>
        /// The route template for the MCP endpoint, relative to the app root.
        /// </summary>
        public const string RouteTemplate = "automate/mcp/{automationId}";
```

- [ ] **Step 4: Run the integration test to verify it passes**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx --filter McpEndpointWiringTests`
Expected: PASS

- [ ] **Step 5: Run the full test suite**

Run: `dotnet test Umbraco.Automate/Umbraco.Automate.slnx`
Expected: All tests pass.

- [ ] **Step 6: Manual verification against the demo site**

Start the demo site (`/demo-site-management` skill, or `scripts/install-demo-site.ps1` if `demos/v18/` doesn't exist yet). In the backoffice, create an automation with the MCP Tool trigger, one text input field, a `LogMessageAction` step bound to `Arguments["<field name>"]`, and publish it. Note its automation ID from the URL. From a terminal:

```bash
curl -X POST "https://localhost:<port>/automate/mcp/<automationId>" \
  -H "Content-Type: application/json" \
  -d '{"jsonrpc":"2.0","id":1,"method":"tools/list"}'
```

Expected: a JSON-RPC response listing exactly one tool, named and described the way the trigger's settings say. Then call `tools/call` with the field's name as an argument and confirm the automation's run appears in the Runs list with the argument value logged.

- [ ] **Step 7: Commit**

```bash
git add Umbraco.Automate/src/Umbraco.Automate.Web/Configuration/UmbracoBuilderExtensions.cs \
        Umbraco.Automate/src/Umbraco.Automate.Web/Constants.cs \
        Umbraco.Automate/tests/Umbraco.Automate.Tests.Integration/McpEndpointWiringTests.cs
git commit -m "feat(api): Wire the MCP server, auth middleware, and rate limiting into the pipeline"
```

**Suggested PR boundary:** Task 7 is its own PR, stacked on Task 5-6's PR — it's the one piece that needed genuinely new test infrastructure and is the first point the whole feature is actually callable end-to-end.

---

## Task 8: Backoffice input-fields editor

**Files:**
- Create: `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/core/components/mcp-input-fields-builder/mcp-input-fields-builder.element.ts`
- Create: `Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/core/components/mcp-input-fields-builder/manifests.ts`
- Modify: wherever `switchCaseBuilderManifests` (or the aggregate `core/components` manifest list it feeds into) is registered, to also register `mcpInputFieldsBuilderManifests`.

**Interfaces:**
- Produces: the `UmbracoAutomate.PropertyEditorUi.McpInputFieldsBuilder` property editor UI, which `McpTriggerSettings.InputFields` (Task 1) already declares via `EditorUiAlias`.

This is a smaller version of the existing `switch-case-builder` pattern (`Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/core/components/switch-case-builder/`) — a repeating list editor over a flat structured row (name/type/description/required) instead of switch-case-builder's nested condition sub-editor. The webhook secret field (`Umb.Automate.WebhookSecretField`) is reused as-is for `McpTriggerSettings.Secret` in Task 1 — no new secret-field work needed, it has no webhook-specific logic despite its name.

- [ ] **Step 1: Find where `switchCaseBuilderManifests` is registered**

Run: `grep -rn "switchCaseBuilderManifests" Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src`

Expected: one import/spread site (likely an aggregate `manifests.ts` a few directories up, e.g. `core/components/manifests.ts` or the top-level `manifests.ts`). Use that exact file and pattern for Step 4 below — do not guess the aggregation point without checking, since getting this wrong means the editor silently never registers.

- [ ] **Step 2: Create the input field row type and element**

```typescript
export interface McpToolInputFieldRow {
    name: string;
    type: "Text" | "Number" | "Boolean";
    description: string | null;
    required: boolean;
}
```

```typescript
import { css, customElement, html, property, repeat } from "@umbraco-cms/backoffice/external/lit";
import { UmbChangeEvent } from "@umbraco-cms/backoffice/event";
import { UmbLitElement } from "@umbraco-cms/backoffice/lit-element";
import type { UmbPropertyEditorUiElement } from "@umbraco-cms/backoffice/property-editor";
import type { McpToolInputFieldRow } from "./types.js";

@customElement("ua-mcp-input-fields-builder")
export class UaMcpInputFieldsBuilderElement extends UmbLitElement implements UmbPropertyEditorUiElement {
    @property({ type: Array })
    value: McpToolInputFieldRow[] = [];

    #cloneValue(): McpToolInputFieldRow[] {
        return structuredClone(this.value ?? []);
    }

    #emitChange() {
        this.dispatchEvent(new UmbChangeEvent());
    }

    #addField() {
        const next = this.#cloneValue();
        next.push({ name: "", type: "Text", description: null, required: false });
        this.value = next;
        this.#emitChange();
    }

    #removeField(index: number) {
        const next = this.#cloneValue();
        next.splice(index, 1);
        this.value = next;
        this.#emitChange();
    }

    #updateField(index: number, patch: Partial<McpToolInputFieldRow>) {
        const next = this.#cloneValue();
        next[index] = { ...next[index], ...patch };
        this.value = next;
        this.#emitChange();
    }

    override render() {
        return html`
            ${repeat(
                this.value ?? [],
                (_, index) => index,
                (field, index) => html`
                    <uui-box class="row">
                        <uui-input
                            label="Name"
                            placeholder="Argument name"
                            .value=${field.name}
                            @input=${(e: InputEvent) => this.#updateField(index, { name: (e.target as HTMLInputElement).value })}
                        ></uui-input>
                        <uui-select
                            .options=${[
                                { name: "Text", value: "Text", selected: field.type === "Text" },
                                { name: "Number", value: "Number", selected: field.type === "Number" },
                                { name: "Boolean", value: "Boolean", selected: field.type === "Boolean" },
                            ]}
                            @change=${(e: CustomEvent) =>
                                this.#updateField(index, { type: (e.target as HTMLSelectElement).value as McpToolInputFieldRow["type"] })}
                        ></uui-select>
                        <uui-input
                            label="Description"
                            placeholder="Tells the agent what this argument is for"
                            .value=${field.description ?? ""}
                            @input=${(e: InputEvent) => this.#updateField(index, { description: (e.target as HTMLInputElement).value })}
                        ></uui-input>
                        <uui-toggle
                            label="Required"
                            ?checked=${field.required}
                            @change=${(e: Event) => this.#updateField(index, { required: (e.target as HTMLInputElement).checked })}
                        ></uui-toggle>
                        <uui-button compact look="secondary" label="Remove field" @click=${() => this.#removeField(index)}>
                            <uui-icon name="icon-trash"></uui-icon>
                        </uui-button>
                    </uui-box>
                `,
            )}
            <uui-button look="secondary" label="Add field" @click=${() => this.#addField()}>
                <uui-icon name="icon-add"></uui-icon>
                Add field
            </uui-button>
        `;
    }

    static override styles = [
        css`
            .row {
                display: flex;
                align-items: center;
                gap: var(--uui-size-space-3);
                margin-bottom: var(--uui-size-space-3);
            }

            .row uui-input:first-child {
                flex: 1;
            }

            .row uui-input:nth-child(3) {
                flex: 2;
            }
        `,
    ];
}

export default UaMcpInputFieldsBuilderElement;

declare global {
    interface HTMLElementTagNameMap {
        "ua-mcp-input-fields-builder": UaMcpInputFieldsBuilderElement;
    }
}
```

Put the `McpToolInputFieldRow` interface in a sibling `types.ts` file in the same folder, mirroring how other components in this directory separate their row type from the element when the type is reused (check `switch-case-builder`'s folder for whether it does this or inlines its `SwitchCase` import from `@automate/core` — follow whichever convention that folder actually uses so this stays consistent, adjusting the import path in the element above to match).

- [ ] **Step 3: Register the manifest**

```typescript
export const MCP_INPUT_FIELDS_BUILDER_UI_ALIAS = "UmbracoAutomate.PropertyEditorUi.McpInputFieldsBuilder";

const mcpInputFieldsBuilder: UmbExtensionManifest = {
    type: "propertyEditorUi",
    alias: MCP_INPUT_FIELDS_BUILDER_UI_ALIAS,
    name: "Automate MCP Input Fields Builder",
    element: () => import("./mcp-input-fields-builder.element.js"),
    meta: {
        label: "MCP Input Fields Builder",
        icon: "icon-plug",
        group: "Automate",
    },
};

export const mcpInputFieldsBuilderManifests: UmbExtensionManifest[] = [mcpInputFieldsBuilder];
```

- [ ] **Step 4: Wire it into the aggregate manifest list**

Using the exact file and pattern found in Step 1, add `mcpInputFieldsBuilderManifests` alongside `switchCaseBuilderManifests` the same way it's already combined there (spread into the same array, or added as another entry to the same `manifests` export — match whatever that file already does).

- [ ] **Step 5: Manual verification**

Start the demo site frontend build (per the demo-site-management skill) and confirm: creating an automation with the MCP Tool trigger shows the input-fields editor, adding/removing/editing rows updates the trigger's saved settings, and reopening the automation shows the previously-saved fields.

- [ ] **Step 6: Commit**

```bash
git add Umbraco.Automate/src/Umbraco.Automate.Web.StaticAssets/Client/src/core/components/mcp-input-fields-builder
git commit -m "feat(frontend): Add MCP input fields builder property editor"
```

**Suggested PR boundary:** Task 8 is its own PR, stacked on Task 7's PR (needs `McpTriggerSettings.InputFields`'s `EditorUiAlias` from Task 1, but is otherwise independent of the backend runtime work) — frontend-only, verified manually rather than by the .NET test suite.

---

## Self-Review Notes

- **Spec coverage:** Goals (built-in trigger, no new package, one tool per automation, waits for result, no new Connection type) — Tasks 1-2, 7. Non-goals respected (no router model, no OAuth, single tool per URL). Endpoint & protocol (route, Streamable HTTP, tools/list/tools/call shape) — Tasks 6-7. Authentication (bearer, optional) — Task 5. Execution & wait semantics (targeted execution, polling not notification, timeout) — Task 6. Error handling table — covered across Tasks 5 (404/401/circuit breaker is pre-existing in `IAutomationExecutor`, not re-checked here since `ExecuteAsync` already enforces it the same way "Run now" does) and 6 (bad input, step failure, timeout). Testing section's named precedents (`ManualTriggerTests`, `WebhookEndpointControllerTests`, `ManualTriggerLogMessageTests`) — followed in Tasks 2, 5, 7 respectively.
- **Placeholder scan:** The one deliberately open item is the exact `HttpServerTransportOptions` member name for stateless mode (Task 7, Step 3) — flagged explicitly as "check the installed version" with both known possible names given, not left as a bare TBD, because the SDK's own naming changed between versions I found cited in different sources during research and no package version is pinned yet at plan-writing time.
- **Type consistency:** `McpTriggerSettings`/`McpTriggerOutput`/`McpToolInputField`/`McpToolInputFieldType` (Task 1) are used with identical names and shapes in Tasks 2, 4, 5, 6, 7, 8. `AutomationMcpTool`'s constructor signature (Task 6) matches every call site in Task 7 and its own test.
