using System.Text.Json.Nodes;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Umbraco.Automate.Core.Http;

namespace Umbraco.Automate.Core.Scripting;

/// <summary>
/// A minimal implementation of the web <c>Response</c> interface returned by <c>fetch</c>. Body
/// reads are capped via <see cref="HttpResponseBodyReader"/> so a script cannot buffer an
/// oversized payload into the engine.
/// </summary>
#pragma warning disable S2325 // instance members mirror the web Response API surface
internal sealed class Response : IDisposable
{
    private readonly HttpResponseMessage _response;
    private readonly Engine _engine;
    private readonly long _maxBodyBytes;

    internal Response(Uri url, HttpResponseMessage response, Engine engine, long maxBodyBytes)
    {
        _response = response;
        _engine = engine;
        _maxBodyBytes = maxBodyBytes;
        Headers = new(response);
        Url = url;
    }

    /// <summary>Gets whether the body has already been consumed.</summary>
    public bool BodyUsed { get; private set; }

    /// <summary>Gets the response headers.</summary>
    public Headers Headers { get; }

    /// <summary>Gets whether the response status is in the 2xx range.</summary>
    public bool Ok => _response.IsSuccessStatusCode;

    /// <summary>Gets whether the response was redirected.</summary>
    public bool Redirected { get; }

    /// <summary>Gets the HTTP status code.</summary>
    public int Status => (int)_response.StatusCode;

    /// <summary>Gets the HTTP status reason phrase.</summary>
    public string? StatusText => _response.ReasonPhrase;

    /// <summary>Gets the response type.</summary>
    public string Type => "cors";

    /// <summary>Gets the response URL.</summary>
    public Uri Url { get; }

    /// <summary>Reads the body as JSON, resolving to the parsed value.</summary>
    public JsValue Json()
    {
        if (BodyUsed) throw new JavaScriptException("body has already been consumed");
        BodyUsed = true;

        var (promise, resolve, reject) = _engine.Advanced.RegisterPromise();
        HttpResponseBodyReader.ReadCappedAsync(_response.Content, _maxBodyBytes, CancellationToken.None)
            .ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    if (task.Result is null)
                    {
                        reject(JsValue.FromObject(_engine, "response body exceeded the maximum allowed size"));
                        return;
                    }

                    try
                    {
                        resolve(JsValue.FromObject(_engine, JsonNode.Parse(task.Result)));
                    }
                    catch (Exception ex)
                    {
                        reject(JsValue.FromObject(_engine, ex.Message));
                    }
                }
                else
                {
                    reject(JsValue.FromObject(_engine, task.Exception?.Message));
                }
            });

        return promise;
    }

    /// <summary>Reads the body as text, resolving to the decoded string.</summary>
    public JsValue Text()
    {
        if (BodyUsed) throw new JavaScriptException("body has already been consumed");
        BodyUsed = true;

        var (promise, resolve, reject) = _engine.Advanced.RegisterPromise();
        HttpResponseBodyReader.ReadCappedAsync(_response.Content, _maxBodyBytes, CancellationToken.None)
            .ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    if (task.Result is null)
                    {
                        reject(JsValue.FromObject(_engine, "response body exceeded the maximum allowed size"));
                        return;
                    }

                    resolve(JsValue.FromObject(_engine, task.Result));
                }
                else
                {
                    reject(JsValue.FromObject(_engine, task.Exception?.Message));
                }
            });

        return promise;
    }

    /// <inheritdoc />
    public void Dispose() => _response.Dispose();
}
#pragma warning restore S2325
