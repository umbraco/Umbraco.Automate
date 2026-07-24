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
    IReadOnlyList<string> Validate(string? script);
}

/// <summary>
/// Compiles the script in a tightly-bounded throwaway engine and checks that it imports cleanly
/// and exports a default function. Does not execute the function body.
/// </summary>
internal sealed class ScriptValidator : IScriptValidator
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(2);

    /// <inheritdoc />
    public IReadOnlyList<string> Validate(string? script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return ["Script is required."];
        }

        // Compile on a background task under a wall-clock backstop: this runs on the save request
        // thread, and the engine's statement-checked limits cannot interrupt pathological top-level
        // code (e.g. a parked await), so the task guarantees the save call stops waiting regardless.
        using var cts = new CancellationTokenSource(CompileTimeout);
        var task = Task.Run(() => CompileAndCheck(script!, cts.Token), cts.Token);

        try
        {
            if (!task.Wait(CompileTimeout + TimeSpan.FromSeconds(1)))
            {
                return ["Script validation timed out. Simplify top-level module code so it loads quickly."];
            }
        }
        catch (AggregateException)
        {
            return ["Script could not be validated."];
        }

        return task.Result;
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
