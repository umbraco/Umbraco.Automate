using Jint;
using Jint.Native;
using Jint.Native.Function;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Umbraco.Automate.Core.Scripting;

/// <summary>
/// Validates a script at author time — before it is saved — so syntax errors and a missing
/// default export are reported immediately rather than only failing at run time.
/// </summary>
public interface IScriptValidator
{
    /// <summary>
    /// Validates the given script. Returns an empty list when valid, otherwise one message per problem.
    /// </summary>
    /// <param name="script">The script to validate.</param>
    /// <param name="cancellationToken">A token to cancel the validation.</param>
    Task<IReadOnlyList<string>> ValidateScriptAsync(string? script, CancellationToken cancellationToken = default);
}

/// <summary>
/// Compiles the script in a tightly-bounded throwaway engine and checks that it imports cleanly
/// and exports a default function. Does not execute the function body.
/// </summary>
internal sealed class ScriptValidator : IScriptValidator
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ValidateScriptAsync(string? script, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return ["Script is required."];
        }

        // Compile on a background task under a wall-clock backstop: the engine's statement-checked
        // limits cannot interrupt pathological top-level code (e.g. a parked await), so the task
        // guarantees the caller stops waiting regardless. Not wrapped in `using` — an overran
        // compile still holds the token, so disposal is deferred to the continuation below.
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(CompileTimeout);

        // The token bounds the compile itself, but must not be Task.Run's creation token: that
        // cancels the work item before it is ever dequeued, failing a valid script whenever the
        // thread pool is slow to start it.
        var task = Task.Run(() => CompileAndCheck(script!, cts.Token), CancellationToken.None);
        _ = task.ContinueWith(
            t =>
            {
                _ = t.Exception; // observe to avoid an unobserved-task-exception
                cts.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        try
        {
            return await task.WaitAsync(CompileTimeout + TimeSpan.FromSeconds(1), cancellationToken);
        }
        catch (TimeoutException)
        {
            return ["Script validation timed out. Simplify top-level module code so it loads quickly."];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Anything CompileAndCheck did not translate into a message itself — report it rather
            // than discarding the only description of what went wrong.
            return [$"Script could not be validated: {ex.Message}"];
        }
    }

    private static IReadOnlyList<string> CompileAndCheck(string script, CancellationToken cancellationToken)
    {
        // `using` lives inside the task body, so the engine is disposed only when this method
        // returns — never out from under a still-running compile if the caller abandons it.
        using var engine = new Engine(options =>
        {
            options.LimitMemory(1_000_000);
            options.MaxStatements(1000);
            options.TimeoutInterval(TimeSpan.FromSeconds(1));
            options.CancellationToken(cancellationToken);
        });

        try
        {
            // Define the globals so top-level references don't throw during module evaluation.
            engine.SetValue("console", new JsConsole { Logger = (_, _) => { } });
            engine.SetValue("Headers", TypeReference.CreateTypeReference<Headers>(engine));
            engine.SetValue("RequestInit", TypeReference.CreateTypeReference<RequestInit>(engine));

            // A callable no-op (not `undefined`) so top-level `fetch(...)` calls compile — whether
            // fetch is actually enabled is a runtime concern, not a validation one.
            engine.SetValue("fetch", (string _, RequestInit? _) => JsValue.Undefined);

            engine.Modules.Add("script", script);
            var module = engine.Modules.Import("script");

            if (module.Get("default") is not Function)
            {
                return ["Script must export a default function."];
            }

            return [];
        }
        catch (JavaScriptException ex)
        {
            // Strip the module-loading prefix Jint adds so the message reads cleanly.
            var message = ex.Error.ToString()
                .Replace("Error while loading module: error in module 'script': ", string.Empty, StringComparison.Ordinal);
            return [message];
        }
        catch (JintException ex)
        {
            return [ex.Message];
        }
    }
}
