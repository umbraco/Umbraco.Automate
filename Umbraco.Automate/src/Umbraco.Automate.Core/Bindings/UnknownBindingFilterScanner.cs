using System.Text.Json;
using Umbraco.Automate.Core.Automations;

namespace Umbraco.Automate.Core.Bindings;

/// <summary>
/// Finds <c>${ }</c> bindings that pipe a value through a filter alias no registered binding filter
/// declares, without evaluating them. Publish validation uses it to reject an automation with a
/// mistyped filter name, and <c>AutomationExecutor</c> uses it to fail a run whose connection
/// filters have one before the workflow starts. Both look filters up through
/// <see cref="BindingEvaluator.IsKnownFilter"/>, so they agree with the run on which names exist.
/// </summary>
internal static class UnknownBindingFilterScanner
{
    /// <summary>
    /// Returns one message per unknown filter alias used by a filter on one of the automation's
    /// connections, including connections from the trigger. <paramref name="include"/> narrows the
    /// check to some connections, such as only those from the trigger.
    /// </summary>
    public static List<string> GetConnectionFilterErrors(
        Automation automation,
        BindingEvaluator evaluator,
        Func<StepConnection, bool>? include = null)
    {
        var errors = new List<string>();
        var stepNames = automation.Steps.ToDictionary(s => s.Id, s => s.Name);

        foreach (var connection in automation.Connections)
        {
            if (connection.Filter is null || include?.Invoke(connection) == false)
            {
                continue;
            }

            var source = connection.SourceStepId == Guid.Empty
                ? "the trigger"
                : $"'{stepNames.GetValueOrDefault(connection.SourceStepId, connection.SourceStepId.ToString())}'";
            var target = stepNames.GetValueOrDefault(connection.TargetStepId, connection.TargetStepId.ToString());

            foreach (var alias in FindUnknownFilterAliases(connection.Filter, evaluator))
            {
                errors.Add($"The filter on the connection from {source} to '{target}' {Describe(alias)}");
            }
        }

        return errors;
    }

    /// <summary>
    /// Finds the distinct filter aliases, in order of first use, that the bindings in a value use
    /// but no registered binding filter declares. A string is scanned as is; anything else (a list
    /// of rows, a condition set) is serialized and each of its string values is scanned on its own,
    /// so a binding is never read across two separate values.
    /// </summary>
    public static List<string> FindUnknownFilterAliases(object? value, BindingEvaluator evaluator)
    {
        var unknown = new List<string>();
        foreach (var text in GetStringValues(value))
        {
            foreach (var (_, _, content) in BindingTokenizer.FindBindings(text))
            {
                foreach (var filter in BindingTokenizer.Tokenize(content).Filters)
                {
                    if (!evaluator.IsKnownFilter(filter.Alias)
                        && !unknown.Contains(filter.Alias, StringComparer.OrdinalIgnoreCase))
                    {
                        unknown.Add(filter.Alias);
                    }
                }
            }
        }

        return unknown;
    }

    /// <summary>
    /// Ends an error message about a filter alias that matched no registered filter. A malformed
    /// alias is not echoed: it is usually argument text after a <c>|</c>, which may be secret.
    /// </summary>
    public static string Describe(string alias)
        => BindingTokenizer.IsWellFormedFilterAlias(alias)
            ? $"uses the binding filter '{alias}', which does not exist."
            : "uses a binding filter name that is not valid. A '|' inside a filter argument starts a new filter.";

    private static List<string> GetStringValues(object? value)
    {
        if (value is null)
        {
            return [];
        }

        if (value is string text)
        {
            return [text];
        }

        var strings = new List<string>();
        CollectStrings(JsonSerializer.SerializeToElement(value, Dispatch.JsonOptions.Settings), strings);
        return strings;
    }

    private static void CollectStrings(JsonElement element, List<string> strings)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                strings.Add(element.GetString()!);
                break;

            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    CollectStrings(property.Value, strings);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    CollectStrings(item, strings);
                }

                break;
        }
    }
}
