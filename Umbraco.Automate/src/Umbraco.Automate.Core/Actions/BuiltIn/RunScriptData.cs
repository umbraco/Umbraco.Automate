using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using Newtonsoft.Json.Linq;

namespace Umbraco.Automate.Core.Actions.BuiltIn;

/// <summary>
/// Builds the <c>data</c> argument handed to a <see cref="RunScriptAction"/> script: the step's
/// binding context (<c>trigger</c>, <c>steps</c>, <c>previous</c>, <c>loop</c>) with any input
/// mappings layered on top as root keys.
/// </summary>
/// <remarks>
/// <para>
/// The paths mirror binding syntax, so <c>${ steps.getMedia.properties.umbracoBytes }</c> in a
/// binding is <c>data.steps.getMedia.properties.umbracoBytes</c> in a script.
/// </para>
/// <para>
/// The result is a detached JSON copy, never a live CLR object: the script cannot reach
/// interop members or mutate run state, and nothing it does to <c>data</c> leaks into later steps.
/// The binding context only ever holds trigger and step outputs — connection credentials live on
/// <see cref="ActionContext.Connection"/>, which is deliberately not included.
/// </para>
/// </remarks>
internal static class RunScriptData
{
    /// <summary>
    /// Nesting beyond this is treated as a cycle. Step outputs are persisted JSON, so real data
    /// never gets close; the guard only exists so a self-referencing object fails cleanly.
    /// </summary>
    internal const int MaxDepth = 64;

    private static readonly string[] _contextKeys = ["trigger", "steps", "previous", "loop"];

    /// <summary>
    /// Builds the script's <c>data</c> argument from <paramref name="context"/>.
    /// </summary>
    /// <exception cref="JsonException">The data nests deeper than <see cref="MaxDepth"/> or contains a cycle.</exception>
    public static JsonObject Build(ActionContext context)
    {
        var data = new JsonObject();

        if (context.BindingData is { } bindingData)
        {
            foreach (var key in _contextKeys)
            {
                if (!bindingData.TryGetValue(key, out var value))
                {
                    continue;
                }

                data[key] = key == "steps" ? ToStepsNode(value) : ToNode(value, 1);
            }
        }

        // Input mappings keep their pre-binding-context meaning: root keys on `data`. They win on
        // a clash so an automation that mapped e.g. `trigger` explicitly behaves as it did.
        foreach (var (key, value) in context.InputData)
        {
            data[key] = ToNode(value, 1);
        }

        return data;
    }

    /// <summary>
    /// The binding context registers each step under both its GUID and its alias, pointing at the
    /// same object. Scripts get one entry per step — keyed by alias when the step has one, as the
    /// binding picker does — so a large output is not copied (and hydrated) twice.
    /// </summary>
    private static JsonNode? ToStepsNode(object? steps)
    {
        if (steps is not IReadOnlyDictionary<string, object?> stepsDict)
        {
            return ToNode(steps, 1);
        }

        var aliased = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var (key, value) in stepsDict)
        {
            if (value is not null && !Guid.TryParse(key, out _))
            {
                aliased.Add(value);
            }
        }

        var node = new JsonObject();
        foreach (var (key, value) in stepsDict)
        {
            if (value is not null && Guid.TryParse(key, out _) && aliased.Contains(value))
            {
                continue;
            }

            node[key] = ToNode(value, 2);
        }

        return node;
    }

    private static JsonNode? ToNode(object? value, int depth)
    {
        if (depth > MaxDepth)
        {
            throw new JsonException(
                $"The step data is nested more than {MaxDepth} levels deep (or refers to itself) and cannot be passed to the script.");
        }

        switch (value)
        {
            case null:
                return null;

            case JsonNode node:
                return node.DeepClone();

            case JsonElement element:
                return JsonSerializer.SerializeToNode(element);

            // WorkflowCore persists run data through Newtonsoft, so outputs read back after a
            // suspension can surface as JTokens — System.Text.Json would serialize their members.
            case JToken token:
                return token.Type is JTokenType.Null or JTokenType.Undefined
                    ? null
                    : JsonNode.Parse(token.ToString(Newtonsoft.Json.Formatting.None));

            case string s:
                return JsonValue.Create(s);

            case IReadOnlyDictionary<string, object?> dict:
                var obj = new JsonObject();
                foreach (var (key, item) in dict)
                {
                    obj[key] = ToNode(item, depth + 1);
                }

                return obj;

            case IDictionary<string, object?> dict:
                var mutableObj = new JsonObject();
                foreach (var (key, item) in dict)
                {
                    mutableObj[key] = ToNode(item, depth + 1);
                }

                return mutableObj;

            case IDictionary dict:
                var untypedObj = new JsonObject();
                foreach (DictionaryEntry entry in dict)
                {
                    untypedObj[entry.Key.ToString() ?? string.Empty] = ToNode(entry.Value, depth + 1);
                }

                return untypedObj;

            case IEnumerable items:
                var array = new JsonArray();
                foreach (var item in items)
                {
                    array.Add(ToNode(item, depth + 1));
                }

                return array;

            default:
                // Primitives, dates, GUIDs and any POCO a trigger or action put in its output —
                // camel-cased to match how step outputs are stored and bound.
                return JsonSerializer.SerializeToNode(value, value.GetType(), Dispatch.JsonOptions.Default);
        }
    }
}
