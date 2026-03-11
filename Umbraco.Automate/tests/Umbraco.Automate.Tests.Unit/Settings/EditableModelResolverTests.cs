using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;

namespace Umbraco.Automate.Tests.Unit.Settings;

public class EditableModelResolverTests
{
    private readonly IConfiguration _configuration;
    private readonly List<IAction> _actions = [];
    private readonly List<ITrigger> _triggers = [];

    public EditableModelResolverTests()
    {
        var configData = new Dictionary<string, string?>
        {
            { "Slack:ApiToken", "xoxb-test-token" },
            { "Slack:BaseUrl", "https://slack.com/api" },
            { "TestSettings:Enabled", "true" },
            { "TestSettings:MaxRetries", "5" },
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
    }

    private EditableModelResolver CreateResolver()
    {
        var actions = new ActionCollection(() => _actions);
        var triggers = new TriggerCollection(() => _triggers);
        return new EditableModelResolver(actions, triggers, _configuration);
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
        var settings = new FakeSettings { ApiToken = "$NonExistent:Key" };
        var resolver = CreateResolver();

        var act = () => resolver.ResolveModel<FakeSettings>("test", settings);

        var exception = Should.Throw<InvalidOperationException>(act);
        exception.Message.ShouldContain("Configuration key");
        exception.Message.ShouldContain("NonExistent:Key");
        exception.Message.ShouldContain("not found");
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

    #endregion

    #region Test models

    public class FakeSettings
    {
        public string? ApiToken { get; set; }
        public string? BaseUrl { get; set; }
        public int MaxRetries { get; set; }
        public bool Enabled { get; set; }
    }

    #endregion
}
