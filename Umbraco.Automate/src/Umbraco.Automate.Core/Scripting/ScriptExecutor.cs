using System.Dynamic;
using System.Globalization;
using System.Text.Json.Nodes;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Jint.Runtime.Interop;
using Microsoft.Extensions.Logging;
using Umbraco.Automate.Core.Security;

namespace Umbraco.Automate.Core.Scripting;

/// <summary>
/// Executes user-authored JavaScript in a sandboxed <see cref="Engine">Jint engine</see>. The
/// script is loaded as an ES module whose <c>default</c> export is invoked with a single argument
/// (the supplied data). Resource limits, an optional SSRF-guarded <c>fetch</c>, and a total
/// execution timeout bound what a script can do.
/// </summary>
/// <remarks>
/// Ported from the Umbraco Headless Orchestration platform's <c>FunctionExecutor</c>. Termination
/// is defended in layers: the per-statement <c>TimeoutInterval</c>, the statement/memory/recursion
/// limits, and a cancellation constraint tied to <see cref="ScriptExecutorOptions.TotalExecutionTimeout"/>
/// that aborts statement execution when the budget is exceeded. Because all of those are only
/// checked when a statement runs, a script parked on a never-resolving promise (which executes no
/// statements) cannot be force-terminated at our budget — but Jint's own <c>UnwrapIfPromise</c>
/// promise-settlement timeout (10s) then aborts it, so the worker thread is reclaimed shortly after
/// and the engine disposed. The outer <c>Task.Run</c> guarantees the <em>caller</em> stops waiting
/// at the (smaller) configured budget regardless; it does not stop the engine work, so the engine is
/// disposed only once its worker task actually completes.
/// </remarks>
public sealed partial class ScriptExecutor(IHttpClientFactory clientFactory, ILogger<ScriptExecutor> logger)
{
    /// <summary>The named <see cref="HttpClient"/> used by <c>fetch</c> when redirects are followed.</summary>
    public const string DefaultHttpClientName = "UmbracoAutomate";

    /// <summary>The named <see cref="HttpClient"/> used by <c>fetch</c> when redirects are not followed.</summary>
    public const string NoRedirectHttpClientName = "UmbracoAutomateNoRedirect";

    /// <summary>
    /// Executes <paramref name="script"/>, invoking its <c>default</c> export with
    /// <paramref name="data"/> and returning the (promise-unwrapped) result.
    /// </summary>
    /// <param name="scriptName">A name for the module (used in error messages).</param>
    /// <param name="script">The JavaScript module source.</param>
    /// <param name="data">The data passed as the single argument to the default export.</param>
    /// <param name="options">Execution options (limits, fetch, timeouts, callbacks).</param>
    /// <param name="cancellationToken">A token to cancel execution.</param>
    /// <returns>
    /// The result serialized to JSON-compatible data (via <c>JSON.stringify</c> semantics:
    /// functions and <c>undefined</c> become <c>null</c>, <c>NaN</c>/<c>Infinity</c> become
    /// <c>null</c>, dates become ISO strings, and circular references fail as a runtime error).
    /// <c>null</c> if execution failed (see <see cref="ScriptExecutorOptions.OnError"/>).
    /// </returns>
    public async ValueTask<JsonNode?> ExecuteAsync(
        string scriptName,
        string script,
        JsonNode? data,
        ScriptExecutorOptions options,
        CancellationToken cancellationToken = default)
    {
        // Linked CTS drives both the poll loop and the engine's cancellation constraint, which
        // aborts statement execution once the total-execution budget is exceeded. Not wrapped in
        // `using` — it is disposed together with the engine by DisposeWhenIdle so a still-running
        // (overran) worker task never observes a disposed token.
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(options.TotalExecutionTimeout);

        var engine = new Engine(engineOptions =>
        {
            engineOptions.LocalTimeZone(TimeZoneInfo.Utc);

            engineOptions.LimitMemory(options.MaxMemoryBytes);
            engineOptions.LimitRecursion(options.MaxRecursionDepth);
            engineOptions.MaxArraySize((uint)options.MaxArraySize);
            engineOptions.MaxStatements(options.MaxStatements);
            engineOptions.TimeoutInterval(options.StatementTimeout);
            engineOptions.CancellationToken(cts.Token);

            engineOptions.Culture = CultureInfo.InvariantCulture;
            engineOptions.ExperimentalFeatures = ExperimentalFeature.TaskInterop;
            engineOptions.Strict = true;

            engineOptions.Interop.TrackObjectWrapperIdentity = false;

            engineOptions.CatchClrExceptions(ex => ex is JavaScriptException);
        });

        Task<JsonNode?>? task = null;
        try
        {
            engine.SetValue("console", new JsConsole
            {
                Logger = (level, args) =>
                {
                    if (options.OnLogMessage is null)
                    {
                        return;
                    }

                    string message = string.Join(" ", args);
                    if (level == "trace")
                    {
                        message = string.Format(CultureInfo.InvariantCulture, "console.trace() {0}\n{1}", message, engine.Advanced.StackTrace);
                    }

                    options.OnLogMessage(new(level, message));
                },
            });

            engine.SetValue("Headers", TypeReference.CreateTypeReference<Headers>(engine));
            engine.SetValue("RequestInit", TypeReference.CreateTypeReference<RequestInit>(engine));

            if (options.AllowFetch)
            {
                engine.SetValue("fetch", async (string url, RequestInit? requestInit = null) =>
                    await FetchAsync(engine, url, requestInit, options));
            }

            JsValue defaultFunction;
            try
            {
                engine.Modules.Add(scriptName, script);
                var module = engine.Modules.Import(scriptName);
                defaultFunction = module.Get("default");
            }
            catch (JavaScriptException ex)
            {
                ReportError(ScriptErrorKind.Compilation, ex.GetJavaScriptErrorString());
                return null;
            }

            var dataValue = JsValue.FromObject(engine, data);

            // Run on a background task so the caller can stop waiting at the budget even when the
            // engine cannot be interrupted (see remarks). Serialize on the same thread while the
            // engine is still exclusively ours, so the result is JSON-safe before it leaves here.
            task = Task.Run(
                () => SerializeResult(engine, engine.Invoke(defaultFunction, dataValue).UnwrapIfPromise()),
                cts.Token);

            // Wait for the script to finish or the budget to expire — no polling. When the CTS
            // fires, the infinite delay completes (cancelled) and Task.WhenAny returns without
            // throwing; the abandoned worker task is dealt with by DisposeWhenIdle.
            await Task.WhenAny(task, Task.Delay(Timeout.InfiniteTimeSpan, cts.Token));

            if (task.IsCompletedSuccessfully)
            {
                return await task;
            }

            if (task.Exception?.InnerException is JavaScriptException jsex)
            {
                ReportError(ScriptErrorKind.Runtime, jsex.GetJavaScriptErrorString());
            }
            else if (task.Exception?.InnerException is PromiseRejectedException prex)
            {
                // An uncaught rejection from an async default export (e.g. an awaited fetch that threw).
                ReportError(ScriptErrorKind.Runtime, prex.RejectedValue.ToString());
            }
            else if (task.Exception?.InnerException is JintException jex)
            {
                // Memory / recursion / statement-count / cancellation-constraint hits.
                ReportError(ScriptErrorKind.Timeout, jex.Message);
            }
            else if (cts.IsCancellationRequested)
            {
                ReportError(
                    ScriptErrorKind.Timeout,
                    cancellationToken.IsCancellationRequested
                        ? "Script execution was cancelled."
                        : "Script execution timed out.");
            }
            else if (task.Exception is not null)
            {
                LogUnexpectedError(task.Exception);
                ReportError(ScriptErrorKind.Unexpected, "Unexpected error.");
            }

            return null;
        }
        finally
        {
            DisposeWhenIdle(engine, cts, task);
        }

        void ReportError(ScriptErrorKind kind, string message) => options.OnError?.Invoke(new(kind, message));
    }

    // Disposes the engine (and its linked CTS) once no worker task is still using it. If the script
    // overran and its task is still running, disposal is deferred to a continuation, because the
    // Jint engine is not thread-safe and disposing it under the live thread would race. Statement-
    // executing runaways are aborted by the cancellation constraint; a pure never-resolving promise
    // is aborted by Jint's UnwrapIfPromise timeout (~10s) — either way the task eventually completes
    // and this continuation disposes the engine then (see the ExecuteAsync remarks).
    private static void DisposeWhenIdle(Engine engine, CancellationTokenSource cts, Task? task)
    {
        if (task is null || task.IsCompleted)
        {
            engine.Dispose();
            cts.Dispose();
            return;
        }

        task.ContinueWith(
            t =>
            {
                _ = t.Exception; // observe to avoid an unobserved-task-exception
                engine.Dispose();
                cts.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static JsonNode? SerializeResult(Engine engine, JsValue value)
    {
        if (value.IsUndefined() || value.IsNull())
        {
            return null;
        }

        // Round-trip through the engine's JSON.stringify so the result obeys JSON semantics:
        // functions/undefined are dropped, NaN/Infinity become null, dates become ISO strings,
        // and circular references throw a JavaScriptException we classify as a runtime error.
        engine.SetValue("__automateResult", value);
        var json = engine.Evaluate("JSON.stringify(__automateResult)");
        return json.IsString() ? JsonNode.Parse(json.AsString()) : null;
    }

    private async Task<Response> FetchAsync(Engine engine, string url, RequestInit? requestInit, ScriptExecutorOptions options)
    {
        var requestUri = new Uri(url);
        if (requestUri.Scheme is not "http" and not "https")
        {
            throw new JavaScriptException("url scheme must be http or https");
        }

        if (options.FetchAllowedHosts.Count > 0
            && !options.FetchAllowedHosts.Contains(requestUri.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new JavaScriptException($"fetch to host '{requestUri.Host}' is not allowed");
        }

        requestInit ??= new();

        var request = new HttpRequestMessage
        {
            RequestUri = requestUri,
            Method = HttpMethod.Parse(requestInit.Method),
        };

        if (requestInit.Body is not null)
        {
            request.Content = new StringContent(requestInit.Body);
        }

        ApplyRequestHeaders(request, requestInit.Headers);

        // When a host allowlist is configured, never auto-follow redirects: a followed redirect
        // would reach a host the allowlist check (which only validated the initial URL) never
        // vetted, so the allowlist must bind the host actually contacted.
        var followRedirects = requestInit.Redirect is "follow" && options.FetchAllowedHosts.Count == 0;
        using var client = followRedirects
            ? clientFactory.CreateClient(DefaultHttpClientName)
            : clientFactory.CreateClient(NoRedirectHttpClientName);

        try
        {
            using var cts = new CancellationTokenSource(options.HttpRequestTimeout);
            var response = await client.SendAsync(request, cts.Token);

            return new Response(requestUri, response, engine, options.MaxResponseBodyBytes);
        }
        catch (HttpRequestException ex) when (ex.InnerException is SsrfException)
        {
            throw new JavaScriptException("http request was blocked");
        }
        catch (TaskCanceledException)
        {
            throw new JavaScriptException("http request timed out");
        }
    }

    private static void ApplyRequestHeaders(HttpRequestMessage request, object? headers)
    {
        switch (headers)
        {
            // new Headers(...)
            case Headers headersObject:
                foreach (var (header, values) in headersObject.AllHeaders)
                {
                    foreach (var value in values)
                    {
                        AddHeader(request, header, value);
                    }
                }

                break;

            // { 'Content-Type': 'text/json' }
            case ExpandoObject expandoHeaders:
                foreach (var (header, value) in expandoHeaders)
                {
                    AddHeader(request, header, value);
                }

                break;

            // [['Content-Type', 'text/json']]
            case object[] headerList:
                foreach (var pair in headerList)
                {
                    if (pair is object[] { Length: 2 } header && header[0]?.ToString() is { } key)
                    {
                        AddHeader(request, key, header[1]);
                    }
                }

                break;
        }
    }

    private static void AddHeader(HttpRequestMessage request, string key, object? value)
    {
        if (key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
        {
            // Content- headers can only appear once, so remove any existing value first.
            request.Content?.Headers.Remove(key);
            request.Content?.Headers.Add(key, value?.ToString());
        }
        else
        {
            request.Headers.Add(key, value?.ToString());
        }
    }

    [LoggerMessage(LogLevel.Error, "Unexpected error while executing script")]
    private partial void LogUnexpectedError(Exception ex);
}
