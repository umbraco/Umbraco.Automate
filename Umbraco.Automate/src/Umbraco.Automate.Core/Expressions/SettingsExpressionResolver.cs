using System.Reflection;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Expressions;

/// <summary>
/// Evaluates <c>${ }</c> expressions in settings POCO string properties
/// that are marked with <c>[Field(SupportsExpressions = true)]</c>.
/// </summary>
internal sealed class SettingsExpressionResolver
{
    private readonly ExpressionEvaluator _expressionEvaluator;

    public SettingsExpressionResolver(ExpressionEvaluator expressionEvaluator)
    {
        _expressionEvaluator = expressionEvaluator;
    }

    /// <summary>
    /// Walks the public string properties of <paramref name="settings"/> and evaluates
    /// <c>${ }</c> expressions on those marked with <c>SupportsExpressions = true</c>.
    /// The settings object is mutated in-place.
    /// </summary>
    public void ResolveExpressions(object settings, IReadOnlyDictionary<string, object?> expressionData)
    {
        var properties = settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (property.PropertyType != typeof(string) || !property.CanRead || !property.CanWrite)
            {
                continue;
            }

            var attr = property.GetCustomAttribute<EditableModelFieldAttribute>();
            if (attr is not { SupportsExpressions: true })
            {
                continue;
            }

            var value = (string?)property.GetValue(settings);
            if (string.IsNullOrEmpty(value))
            {
                continue;
            }

            var resolved = _expressionEvaluator.Evaluate(value, expressionData);
            property.SetValue(settings, resolved);
        }
    }
}
