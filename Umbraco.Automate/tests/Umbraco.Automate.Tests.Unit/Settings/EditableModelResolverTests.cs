using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Actions.BuiltIn;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Tests.Unit.Settings;

public class EditableModelResolverTests
{
    private readonly IConfiguration _configuration;

    public EditableModelResolverTests()
    {
        var configData = new Dictionary<string, string?>
        {
            { "Slack:ApiToken", "xoxb-test-token" },
            { "Slack:BaseUrl", "https://slack.com/api" },
            { "TestSettings:Enabled", "true" },
            { "TestSettings:MaxRetries", "5" },
            { "Umbraco:Automate:Secrets:SlackToken", "xoxb-secret-token" },
            { "Umbraco:Automate:Variables:BaseUrl", "https://env.example.com" },
            // A key outside the sanctioned sections — must not be resolvable from settings.
            { "OutOfScope:SomeValue", "value-outside-allowed-sections" },
            // Sits just outside the Umbraco:Automate:Secrets prefix boundary.
            { "Umbraco:Automate:SecretsBackup:Token", "should-not-resolve" },
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    // Existing mechanics tests reference $Slack:* and $TestSettings:* keys, so the default
    // helper allows those prefixes. Pass explicit prefixes to exercise the allow-list itself.
    private EditableModelResolver CreateResolver(params string[] allowedPrefixes)
    {
        var prefixes = allowedPrefixes.Length > 0
            ? allowedPrefixes
            : ["Slack", "TestSettings"];

        var options = Options.Create(new AutomateOptions { AllowedConfigurationKeyPrefixes = prefixes });
        return new EditableModelResolver(_configuration, options);
    }

    #region ResolveModel<TModel> — Null handling

    [Fact]
    public void ResolveModel_WithNullData_ReturnsNull()
    {
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", null);

        result.ShouldBeNull();
    }

    #endregion

    #region ResolveModel<TModel> — Already typed data

    [Fact]
    public void ResolveModel_WithAlreadyTypedData_ReturnsNewInstance()
    {
        var settings = new FakeSettings { ApiToken = "direct-token" };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", settings);

        result.ShouldNotBeNull();
        result.ShouldNotBeSameAs(settings);
        result!.ApiToken.ShouldBe("direct-token");
    }

    [Fact]
    public void ResolveModel_WithAlreadyTypedData_DoesNotMutateOriginal()
    {
        var settings = new FakeSettings { ApiToken = "$Slack:ApiToken" };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", settings);

        settings.ApiToken.ShouldBe("$Slack:ApiToken");
        result.ShouldNotBeNull();
        result!.ApiToken.ShouldBe("xoxb-test-token");
    }

    [Fact]
    public void ResolveModel_WithAlreadyTypedData_ResolvesConfigurationVariables()
    {
        var settings = new FakeSettings { ApiToken = "$Slack:ApiToken" };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", settings);

        result.ShouldNotBeNull();
        result!.ApiToken.ShouldBe("xoxb-test-token");
    }

    #endregion

    #region ResolveModel<TModel> — JsonElement deserialization

    [Fact]
    public void ResolveModel_WithJsonElement_DeserializesCorrectly()
    {
        var json = """{"apiToken": "direct-token", "baseUrl": "https://custom.api.com", "maxRetries": 10}""";
        var jsonElement = JsonDocument.Parse(json).RootElement;
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", jsonElement);

        result.ShouldNotBeNull();
        result!.ApiToken.ShouldBe("direct-token");
        result.BaseUrl.ShouldBe("https://custom.api.com");
        result.MaxRetries.ShouldBe(10);
    }

    [Fact]
    public void ResolveModel_WithJsonElement_ResolvesConfigurationVariables()
    {
        var json = """{"apiToken": "$Slack:ApiToken", "baseUrl": "$Slack:BaseUrl"}""";
        var jsonElement = JsonDocument.Parse(json).RootElement;
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", jsonElement);

        result.ShouldNotBeNull();
        result!.ApiToken.ShouldBe("xoxb-test-token");
        result.BaseUrl.ShouldBe("https://slack.com/api");
    }

    #endregion

    #region ResolveModel<TModel> — Fallback JSON serialization

    [Fact]
    public void ResolveModel_WithAnonymousObject_FallsBackToJsonSerialization()
    {
        var settings = new { ApiToken = "anon-token", BaseUrl = "https://anon.api.com" };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", settings);

        result.ShouldNotBeNull();
        result!.ApiToken.ShouldBe("anon-token");
        result.BaseUrl.ShouldBe("https://anon.api.com");
    }

    #endregion

    #region ResolveModel<TModel> — Configuration variable errors

    [Fact]
    public void ResolveModel_WithMissingConfigKey_ThrowsInvalidOperationException()
    {
        // Key is under an allowed prefix but absent from configuration — must reach the
        // "not found" path rather than the allow-list rejection.
        var settings = new FakeSettings { ApiToken = "$Slack:NonExistentKey" };
        var resolver = CreateResolver();

        var act = () => resolver.ResolveModel<FakeSettings>("test", settings);

        var exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain("Configuration key");
        exception.Message.ShouldContain("Slack:NonExistentKey");
        exception.Message.ShouldContain("not found");
    }

    #endregion

    #region ResolveModel<TModel> — Configuration key allow-list

    [Fact]
    public void ResolveModel_WithConfigKeyOutsideAllowedPrefix_Throws()
    {
        // A key outside the allowed sections exists in configuration but must stay unreachable.
        var settings = new FakeSettings { ApiToken = "$OutOfScope:SomeValue" };
        var resolver = CreateResolver(); // allows Slack/TestSettings only

        var act = () => resolver.ResolveModel<FakeSettings>("test", settings);

        var exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain("OutOfScope:SomeValue");
        exception.Message.ShouldContain("not permitted");
        // The configured value itself must never leak into the error.
        exception.Message.ShouldNotContain("value-outside-allowed-sections");
    }

    [Fact]
    public void ResolveModel_WithConfigKeyUnderAllowedPrefix_Resolves()
    {
        // Secret key referenced from a sensitive field — the sanctioned use.
        var settings = new FakeSettings { SecretField = "$Umbraco:Automate:Secrets:SlackToken" };
        var resolver = CreateResolver("Umbraco:Automate:Secrets");

        var result = resolver.ResolveModel<FakeSettings>("test", settings);

        result.ShouldNotBeNull();
        result!.SecretField.ShouldBe("xoxb-secret-token");
    }

    [Fact]
    public void ResolveModel_AllowedPrefixMatchingIsSegmentAware_Throws()
    {
        // Umbraco:Automate:SecretsBackup must NOT be admitted by the
        // Umbraco:Automate:Secrets prefix — the boundary is the ':' segment separator.
        var settings = new FakeSettings { ApiToken = "$Umbraco:Automate:SecretsBackup:Token" };
        var resolver = CreateResolver("Umbraco:Automate:Secrets");

        var act = () => resolver.ResolveModel<FakeSettings>("test", settings);

        var exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain("not permitted");
    }

    [Fact]
    public void ResolveModel_WithDefaultOptions_RejectsKeysOutsideAllowedSections()
    {
        // Secure by default: with no configured prefixes the only resolvable sections are
        // Umbraco:Automate:Secrets and Umbraco:Automate:Variables, so an arbitrary key is rejected.
        var settings = new FakeSettings { ApiToken = "$Slack:ApiToken" };
        var options = Options.Create(new AutomateOptions());
        var resolver = new EditableModelResolver(_configuration, options);

        var act = () => resolver.ResolveModel<FakeSettings>("test", settings);

        Should.Throw<InvalidOperationException>(act)
            .Message.ShouldContain("not permitted");
    }

    [Fact]
    public void ResolveModel_WithDefaultOptions_ResolvesSecretsAndVariablesSections()
    {
        // Both default sections resolve out of the box: a Secret into a sensitive field,
        // a Variable into an ordinary field.
        var settings = new FakeSettings
        {
            SecretField = "$Umbraco:Automate:Secrets:SlackToken",
            BaseUrl = "$Umbraco:Automate:Variables:BaseUrl",
        };
        var options = Options.Create(new AutomateOptions());
        var resolver = new EditableModelResolver(_configuration, options);

        var result = resolver.ResolveModel<FakeSettings>("test", settings);

        result.ShouldNotBeNull();
        result!.SecretField.ShouldBe("xoxb-secret-token");
        result.BaseUrl.ShouldBe("https://env.example.com");
    }

    #endregion

    #region ResolveModel<TModel> — Secret keys restricted to sensitive fields

    [Fact]
    public void ResolveModel_SecretKeyInNonSensitiveField_Throws()
    {
        // A secret key may only resolve into a sensitive field; referencing it from a
        // non-sensitive field is rejected.
        var settings = new FakeSettings { ApiToken = "$Umbraco:Automate:Secrets:SlackToken" };
        var options = Options.Create(new AutomateOptions());
        var resolver = new EditableModelResolver(_configuration, options);

        var act = () => resolver.ResolveModel<FakeSettings>("test", settings);

        var exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain("secret");
        exception.Message.ShouldContain("sensitive field");
        exception.Message.ShouldNotContain("xoxb-secret-token");
    }

    [Fact]
    public void ResolveModel_SecretKeyInSensitiveField_Resolves()
    {
        var settings = new FakeSettings { SecretField = "$Umbraco:Automate:Secrets:SlackToken" };
        var options = Options.Create(new AutomateOptions());
        var resolver = new EditableModelResolver(_configuration, options);

        var result = resolver.ResolveModel<FakeSettings>("test", settings);

        result.ShouldNotBeNull();
        result!.SecretField.ShouldBe("xoxb-secret-token");
    }

    [Fact]
    public void ResolveModel_VariablesKeyInNonSensitiveField_Resolves()
    {
        // Variables are not secret, so they are unrestricted by field sensitivity.
        var settings = new FakeSettings { BaseUrl = "$Umbraco:Automate:Variables:BaseUrl" };
        var options = Options.Create(new AutomateOptions());
        var resolver = new EditableModelResolver(_configuration, options);

        var result = resolver.ResolveModel<FakeSettings>("test", settings);

        result.ShouldNotBeNull();
        result!.BaseUrl.ShouldBe("https://env.example.com");
    }

    #endregion

    #region ResolveModel<TModel> — Binding expressions

    [Fact]
    public void ResolveModel_WithBindingExpression_LeavesValueUnresolved()
    {
        var settings = new FakeSettings { ApiToken = "${ trigger.contentName }" };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", settings);

        result.ShouldNotBeNull();
        result!.ApiToken.ShouldBe("${ trigger.contentName }");
    }

    #endregion

    #region ResolveModel<TModel> — Non-string config variables

    [Fact]
    public void ResolveModel_NonStringProperties_PassThroughUnchanged()
    {
        var settings = new FakeSettings
        {
            ApiToken = "test-token",
            MaxRetries = 10,
            Enabled = true,
        };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", settings);

        result.ShouldNotBeNull();
        result!.MaxRetries.ShouldBe(10);
        result.Enabled.ShouldBeTrue();
    }

    #endregion

    #region ResolveModel (non-generic) — Runtime type

    [Fact]
    public void ResolveModel_NonGeneric_DeserializesCorrectly()
    {
        var json = """{"apiToken": "runtime-token"}""";
        var jsonElement = JsonDocument.Parse(json).RootElement;
        var resolver = CreateResolver();

        var result = resolver.ResolveModel("test", typeof(FakeSettings), jsonElement);

        result.ShouldNotBeNull();
        result.ShouldBeOfType<FakeSettings>();
        ((FakeSettings)result!).ApiToken.ShouldBe("runtime-token");
    }

    [Fact]
    public void ResolveModel_NonGeneric_WithNullData_ReturnsNull()
    {
        var resolver = CreateResolver();

        var result = resolver.ResolveModel("test", typeof(FakeSettings), null);

        result.ShouldBeNull();
    }

    [Fact]
    public void ResolveModel_NonGeneric_ResolvesConfigurationVariables()
    {
        var settings = new FakeSettings { ApiToken = "$Slack:ApiToken" };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel("test", typeof(FakeSettings), settings);

        result.ShouldNotBeNull();
        ((FakeSettings)result!).ApiToken.ShouldBe("xoxb-test-token");
    }

    #endregion

    #region ResolveModel — Dictionary<string, object?> input (step settings at runtime)

    [Fact]
    public void ResolveModel_WithDictionary_DeserializesCorrectly()
    {
        // This is what StepConfiguration.Settings actually looks like at runtime.
        var dict = new Dictionary<string, object?>
        {
            ["apiToken"] = "dict-token",
            ["baseUrl"] = "https://dict.api.com",
        };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", dict);

        result.ShouldNotBeNull();
        result!.ApiToken.ShouldBe("dict-token");
        result.BaseUrl.ShouldBe("https://dict.api.com");
    }

    [Fact]
    public void ResolveModel_WithDictionary_ResolvesConfigurationVariables()
    {
        var dict = new Dictionary<string, object?>
        {
            ["apiToken"] = "$Slack:ApiToken",
        };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", dict);

        result.ShouldNotBeNull();
        result!.ApiToken.ShouldBe("xoxb-test-token");
    }

    [Fact]
    public void ResolveModel_NonGeneric_WithDictionary_DeserializesCorrectly()
    {
        var dict = new Dictionary<string, object?>
        {
            ["apiToken"] = "dict-token",
        };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel("test", typeof(FakeSettings), dict);

        result.ShouldNotBeNull();
        ((FakeSettings)result!).ApiToken.ShouldBe("dict-token");
    }

    [Fact]
    public void ResolveModel_WithDictionaryContainingJsonElements_DeserializesCorrectly()
    {
        // After JSON round-trip, dictionary values are JsonElements, not raw strings.
        var json = """{"apiToken":"json-elem-token","maxRetries":3}""";
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, object?>>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", deserialized);

        result.ShouldNotBeNull();
        result!.ApiToken.ShouldBe("json-elem-token");
        result.MaxRetries.ShouldBe(3);
    }

    [Fact]
    public void ResolveModel_WithDictionaryContainingSingleElementArray_UnwrapsToScalar()
    {
        // Reproduces the FindContentSettings.matchMode bug (issue #52): the Umbraco Dropdown
        // editor persists single selections as ["StartsWith"], but settings POCOs declare
        // scalar strings. The exact runtime shape is a step Settings Dictionary whose values
        // are JsonElements after the DB round-trip — must flow through the array converter.
        var json = """{"name":"Homepage","matchMode":["StartsWith"]}""";
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, object?>>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FindContentSettings>("findContent", deserialized);

        result.ShouldNotBeNull();
        result!.Name.ShouldBe("Homepage");
        result.MatchMode.ShouldBe("StartsWith");
    }

    [Fact]
    public void ResolveModel_WithDictionaryStringEncodedNumber_DeserializesToInt()
    {
        // Reproduces issue #34: an int? field rendered through the default TextBox editor
        // persists "24" (string) instead of 24 (number). NumberHandling.AllowReadingFromString
        // on JsonOptions.Settings should let it round-trip.
        var dict = new Dictionary<string, object?>
        {
            ["apiToken"] = "irrelevant",
            ["maxRetries"] = "7",
        };
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", dict);

        result.ShouldNotBeNull();
        result!.MaxRetries.ShouldBe(7);
    }

    [Fact]
    public void ResolveModel_WithDictionaryStringEncodedNumber_FromJsonElements_DeserializesToInt()
    {
        // Same scenario but after a JSON round-trip — values are JsonElement(String), which
        // is what actually arrives at runtime from the persisted automation definition.
        var json = """{"apiToken":"irrelevant","maxRetries":"7"}""";
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, object?>>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<FakeSettings>("test", deserialized);

        result.ShouldNotBeNull();
        result!.MaxRetries.ShouldBe(7);
    }

    [Fact]
    public void ResolveModel_RequestApprovalSettings_WithStringEncodedTimeoutHours_DeserializesCorrectly()
    {
        // Issue #34: an int? field rendered as a TextBox (because the schema builder didn't
        // infer a default EditorUiAlias) persists "24" as a string. AllowReadingFromString
        // lets it round-trip even for legacy automations saved before the schema-builder fix.
        var json = """{"prompt":"Please approve","timeoutHours":"24"}""";
        var deserialized = JsonSerializer.Deserialize<Dictionary<string, object?>>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var resolver = CreateResolver();

        var result = resolver.ResolveModel<RequestApprovalSettings>(
            RequestApprovalAction.ApprovalActionAlias, deserialized);

        result.ShouldNotBeNull();
        result!.Prompt.ShouldBe("Please approve");
        result.TimeoutHours.ShouldBe(24);
    }

    [Fact]
    public void ResolveModel_WithIncompatibleValue_IncludesInnerExceptionDetailsInMessage()
    {
        // Non-numeric string still can't be coerced to int even with AllowReadingFromString —
        // verify the wrapping exception names the inner failure type, includes a JSON path
        // marker, and preserves the original exception so diagnostics aren't lost.
        var dict = new Dictionary<string, object?>
        {
            ["maxRetries"] = "not-a-number",
        };
        var resolver = CreateResolver();

        var act = () => resolver.ResolveModel<FakeSettings>("requestApproval", dict);

        var exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain("requestApproval");
        exception.Message.ShouldContain(nameof(FakeSettings));
        exception.Message.ShouldContain("JsonException");
        exception.Message.ShouldContain("at '$");
        exception.InnerException.ShouldNotBeNull();
        exception.InnerException.ShouldBeOfType<JsonException>();
    }

    #endregion

    #region Test models

    public class FakeSettings
    {
        public string? ApiToken { get; set; }
        public string? BaseUrl { get; set; }
        public int MaxRetries { get; set; }
        public bool Enabled { get; set; }

        [Field(IsSensitive = true)]
        public string? SecretField { get; set; }
    }

    #endregion
}
