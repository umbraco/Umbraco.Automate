using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Moq.Protected;
using Shouldly;
using Umbraco.Automate.Core.Scripting;
using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Tests.Unit.Scripting;

public class ScriptExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_ReturnsScriptResult()
    {
        var (executor, options) = Create();

        var result = await executor.ExecuteAsync(
            "script",
            "export default function (data) { return data.value * 2 }",
            JsonNode.Parse("""{ "value": 21 }"""),
            options);

        result.ShouldNotBeNull();
        result!.GetValue<int>().ShouldBe(42);
    }

    [Fact]
    public async Task ExecuteAsync_AwaitsPromiseResult()
    {
        var (executor, options) = Create();

        var result = await executor.ExecuteAsync(
            "script",
            "export default async function (data) { return await Promise.resolve(data.value) }",
            JsonNode.Parse("""{ "value": "hi" }"""),
            options);

        result!.GetValue<string>().ShouldBe("hi");
    }

    [Fact]
    public async Task ExecuteAsync_CapturesConsoleMessagesInOrder()
    {
        var messages = new List<LogMessage>();
        var (executor, options) = Create();
        options.OnLogMessage = messages.Add;

        await executor.ExecuteAsync(
            "script",
            """
            export default function () {
                console.log('first');
                console.warn('second');
                return null;
            }
            """,
            null,
            options);

        messages.Select(m => (m.Level, m.Message))
            .ShouldBe([("log", "first"), ("warn", "second")]);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidSyntax_ReportsCompilationError()
    {
        ScriptError? error = null;
        var (executor, options) = Create();
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync("script", "export default function ( {", null, options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Compilation);
    }

    [Fact]
    public async Task ExecuteAsync_ThrownError_ReportsRuntimeError()
    {
        ScriptError? error = null;
        var (executor, options) = Create();
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync(
            "script",
            "export default function () { throw new Error('boom') }",
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Runtime);
    }

    [Fact]
    public async Task ExecuteAsync_NeverResolvingPromise_TimesOut()
    {
        // The engine's per-statement timeout cannot interrupt this — only the total-execution
        // timeout backstop can. This is the single most important guarantee of the executor.
        ScriptError? error = null;
        var (executor, options) = Create();
        options.TotalExecutionTimeout = TimeSpan.FromMilliseconds(300);
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync(
            "script",
            "export default async function () { await new Promise(() => {}) }",
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Timeout);
    }

    [Fact]
    public async Task ExecuteAsync_InfiniteLoop_HitsStatementLimit()
    {
        ScriptError? error = null;
        var (executor, options) = Create();
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync(
            "script",
            "export default function () { while (true) { let x = 1 } }",
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Timeout);
    }

    [Fact]
    public async Task ExecuteAsync_InfiniteRecursion_HitsRecursionLimit()
    {
        ScriptError? error = null;
        var (executor, options) = Create();
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync(
            "script",
            "const f = () => f(); export default function () { f() }",
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Timeout);
    }

    [Fact]
    public async Task ExecuteAsync_FetchDisabled_FetchIsUndefined()
    {
        var (executor, options) = Create();
        options.AllowFetch = false;

        var result = await executor.ExecuteAsync(
            "script",
            "export default function () { return typeof fetch }",
            null,
            options);

        result!.GetValue<string>().ShouldBe("undefined");
    }

    [Fact]
    public async Task ExecuteAsync_Fetch_ReturnsResponseText()
    {
        var (executor, options) = Create(HttpStatusCode.OK, "hello world");

        var result = await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                const response = await fetch('https://example.org/data');
                return await response.text();
            }
            """,
            null,
            options);

        result!.GetValue<string>().ShouldBe("hello world");
    }

    [Fact]
    public async Task ExecuteAsync_FetchBlockedBySsrf_SurfacesAsRuntimeError()
    {
        ScriptError? error = null;
        var (executor, options) = Create(ssrfBlocked: true);
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                await fetch('https://169.254.169.254/latest/meta-data');
                return 'should not reach here';
            }
            """,
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Runtime);
        error!.Value.Message.ShouldContain("blocked");
    }

    [Fact]
    public async Task ExecuteAsync_LongRunningLoop_AbortedByTotalTimeoutBeforeStatementLimit()
    {
        // High statement limit so the statement cap can't be what stops it — the cancellation
        // constraint tied to the (short) total timeout must abort the loop.
        ScriptError? error = null;
        var (executor, options) = Create();
        options.MaxStatements = int.MaxValue;
        options.TotalExecutionTimeout = TimeSpan.FromMilliseconds(300);
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync(
            "script",
            "export default function () { let n = 0; while (true) { n++; } }",
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Timeout);
    }

    [Fact]
    public async Task ExecuteAsync_FetchHostNotInAllowlist_IsBlocked()
    {
        ScriptError? error = null;
        var (executor, options) = Create(HttpStatusCode.OK, "data");
        options.FetchAllowedHosts = ["trusted.example.com"];
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                await fetch('https://evil.example.org/data');
                return 'unreachable';
            }
            """,
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Runtime);
        error!.Value.Message.ShouldContain("not allowed");
    }

    [Fact]
    public async Task ExecuteAsync_RespectsConfiguredStatementLimit()
    {
        ScriptError? error = null;
        var (executor, options) = Create();
        options.MaxStatements = 50;
        options.OnError = e => error = e;

        // A loop that would complete well within the default 10k limit but exceeds a tight one.
        var result = await executor.ExecuteAsync(
            "script",
            "export default function () { let n = 0; for (let i = 0; i < 1000; i++) { n += i } return n }",
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Timeout);
    }

    [Fact]
    public async Task ExecuteAsync_NestedObjectResult_RoundTripsAsJson()
    {
        var (executor, options) = Create();

        var result = await executor.ExecuteAsync(
            "script",
            "export default () => ({ a: { b: [10, 20, 30] }, name: 'x' })",
            null,
            options);

        result.ShouldNotBeNull();
        result!["a"]!["b"]![1]!.GetValue<int>().ShouldBe(20);
        result["name"]!.GetValue<string>().ShouldBe("x");
    }

    [Fact]
    public async Task ExecuteAsync_FunctionResult_SerializesToNull()
    {
        var (executor, options) = Create();

        // JSON.stringify of a function yields undefined -> null result.
        var result = await executor.ExecuteAsync("script", "export default () => () => {}", null, options);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_NaNValue_SerializesToJsonNull()
    {
        var (executor, options) = Create();

        var result = await executor.ExecuteAsync("script", "export default () => ({ n: NaN })", null, options);

        result.ShouldNotBeNull();
        (result!["n"] is null).ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_CircularReference_ReportsRuntimeError()
    {
        ScriptError? error = null;
        var (executor, options) = Create();
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync(
            "script",
            "export default () => { const o = {}; o.self = o; return o; }",
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Runtime);
    }

    [Fact]
    public async Task ExecuteAsync_FetchJson_ParsesResponseBody()
    {
        var (executor, options) = Create(HttpStatusCode.OK, """{ "value": 7 }""");

        var result = await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                const r = await fetch('https://example.org/data');
                const body = await r.json();
                return body.value * 6;
            }
            """,
            null,
            options);

        result!.GetValue<int>().ShouldBe(42);
    }

    [Fact]
    public async Task ExecuteAsync_FetchPost_SendsMethodAndBody()
    {
        HttpRequestMessage? captured = null;
        var (executor, options) = CreateCapturing(req => captured = req);

        await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                await fetch('https://example.org/submit', { method: 'POST', body: 'payload' });
                return null;
            }
            """,
            null,
            options);

        captured.ShouldNotBeNull();
        captured!.Method.ShouldBe(HttpMethod.Post);
        (await captured.Content!.ReadAsStringAsync()).ShouldBe("payload");
    }

    [Fact]
    public async Task ExecuteAsync_FetchHeadersAsObject_AppliesHeaders()
    {
        HttpRequestMessage? captured = null;
        var (executor, options) = CreateCapturing(req => captured = req);

        await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                await fetch('https://example.org', { headers: { 'X-Test': 'abc' } });
                return null;
            }
            """,
            null,
            options);

        captured!.Headers.GetValues("X-Test").ShouldContain("abc");
    }

    [Fact]
    public async Task ExecuteAsync_FetchHeadersAsArray_AppliesHeaders()
    {
        HttpRequestMessage? captured = null;
        var (executor, options) = CreateCapturing(req => captured = req);

        await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                await fetch('https://example.org', { headers: [['X-Test', 'abc']] });
                return null;
            }
            """,
            null,
            options);

        captured!.Headers.GetValues("X-Test").ShouldContain("abc");
    }

    [Fact]
    public async Task ExecuteAsync_FetchHeadersAsHeadersObject_AppliesHeaders()
    {
        HttpRequestMessage? captured = null;
        var (executor, options) = CreateCapturing(req => captured = req);

        await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                const h = new Headers();
                h.append('X-Test', 'abc');
                await fetch('https://example.org', { headers: h });
                return null;
            }
            """,
            null,
            options);

        captured!.Headers.GetValues("X-Test").ShouldContain("abc");
    }

    [Fact]
    public async Task ExecuteAsync_FetchLowercaseMethod_IsNormalised()
    {
        // The web fetch API normalises method case; silently falling back to GET would send the
        // body on the wrong method and report success.
        HttpRequestMessage? captured = null;
        var (executor, options) = CreateCapturing(req => captured = req);

        await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                await fetch('https://example.org/submit', { method: 'post', body: 'payload' });
                return null;
            }
            """,
            null,
            options);

        captured!.Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task ExecuteAsync_FetchContentHeaderWithBody_AppliesToContent()
    {
        HttpRequestMessage? captured = null;
        var (executor, options) = CreateCapturing(req => captured = req);

        await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                await fetch('https://example.org', {
                    method: 'POST',
                    body: '{}',
                    headers: {
                        'Content-Type': 'application/json',
                        'Last-Modified': 'Wed, 21 Oct 2015 07:28:00 GMT',
                    },
                });
                return null;
            }
            """,
            null,
            options);

        captured!.Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");

        // "Last-Modified" is a content header despite not starting with "Content-".
        captured.Content.Headers.LastModified.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_FetchContentHeaderWithoutBody_ReportsError()
    {
        // There is nowhere to put a content header on a bodyless request — say so rather than
        // dropping it and sending a request the receiver will reject.
        ScriptError? error = null;
        var (executor, options) = CreateCapturing(_ => { });
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                await fetch('https://example.org', { method: 'POST', headers: { 'Content-Type': 'application/json' } });
                return null;
            }
            """,
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Runtime);
        error!.Value.Message.ShouldContain("Content-Type");
    }

    [Fact]
    public async Task ExecuteAsync_FetchRedirectError_RejectsRedirectedResponse()
    {
        ScriptError? error = null;
        var (executor, options) = Create(HttpStatusCode.Found);
        options.OnError = e => error = e;

        var result = await executor.ExecuteAsync(
            "script",
            """
            export default async function () {
                await fetch('https://example.org', { redirect: 'error' });
                return 'unreachable';
            }
            """,
            null,
            options);

        result.ShouldBeNull();
        error!.Value.Kind.ShouldBe(ScriptErrorKind.Runtime);
        error!.Value.Message.ShouldContain("redirected");
    }

    [Fact]
    public async Task ExecuteAsync_ResultSerialization_DoesNotExposeHostGlobals()
    {
        // The getter runs during JSON.stringify — the point at which the executor used to have a
        // temporary global in scope.
        var (executor, options) = Create();

        var result = await executor.ExecuteAsync(
            "script",
            "export default () => ({ get leaked() { return typeof __automateResult } })",
            null,
            options);

        result!["leaked"]!.GetValue<string>().ShouldBe("undefined");
    }

    private static (ScriptExecutor Executor, ScriptExecutorOptions Options) CreateCapturing(Action<HttpRequestMessage> onRequest)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => onRequest(req))
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });

        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var executor = new ScriptExecutor(factory.Object, NullLogger<ScriptExecutor>.Instance);
        return (executor, new ScriptExecutorOptions { AllowFetch = true });
    }

    private static (ScriptExecutor Executor, ScriptExecutorOptions Options) Create(
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string body = "",
        bool ssrfBlocked = false)
    {
        var handler = new Mock<HttpMessageHandler>();
        var setup = handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());

        if (ssrfBlocked)
        {
            setup.ThrowsAsync(new HttpRequestException("blocked", new SsrfException("blocked")));
        }
        else
        {
            setup.ReturnsAsync(new HttpResponseMessage(statusCode) { Content = new StringContent(body) });
        }

        var client = new HttpClient(handler.Object);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(client);

        var executor = new ScriptExecutor(factory.Object, NullLogger<ScriptExecutor>.Instance);
        var options = new ScriptExecutorOptions { AllowFetch = true };
        return (executor, options);
    }
}
