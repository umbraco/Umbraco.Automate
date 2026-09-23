using System.Collections;
using System.Reflection;
using Umbraco.Automate.Core.Settings;

namespace Umbraco.Automate.Core.Bindings;

/// <summary>
/// Evaluates <c>${ }</c> bindings in settings POCO string properties
/// that are marked with <c>[Field(SupportsBindings = true)]</c>.
/// </summary>
internal sealed class SettingsBindingResolver
{
    private readonly BindingEvaluator _bindingEvaluator;

    public SettingsBindingResolver(BindingEvaluator bindingEvaluator)
    {
        _bindingEvaluator = bindingEvaluator;
    }

    /// <summary>
    /// Walks the public properties of <paramref name="settings"/> and evaluates <c>${ }</c> bindings
    /// on those marked with <c>SupportsBindings = true</c>. Supports plain <c>string</c> properties
    /// and any property whose value implements <see cref="IList{T}"/> of <c>string</c> — covering
    /// <c>List&lt;string&gt;</c>, <c>string[]</c>, and other settable string collections alike,
    /// rather than enumerating concrete collection types one by one. A list of objects is walked
    /// one level deeper, resolving each item's own string properties, which is what makes
    /// <c>${ }</c> work inside a key/value row.
    /// The settings object is mutated in-place.
    /// </summary>
    public void ResolveBindings(object settings, IReadOnlyDictionary<string, object?> bindingData)
    {
        var properties = settings.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (var property in properties)
        {
            if (!property.CanRead)
            {
                continue;
            }

            var attr = property.GetCustomAttribute<EditableModelFieldAttribute>();
            if (attr is not { SupportsBindings: true })
            {
                continue;
            }

            switch (property.GetValue(settings))
            {
                case string value when property.CanWrite && !string.IsNullOrEmpty(value):
                    property.SetValue(settings, _bindingEvaluator.Evaluate(value, bindingData));
                    break;

                case IList<string> list:
                    ResolveListBindings(list, bindingData);
                    break;

                // A list of rows rather than of strings — the HTTP Request action's headers and
                // form fields, for one. Each row's own string properties are resolved, so a
                // ${ } written into a key/value row behaves like one written into a text field.
                case IList itemList:
                    ResolveItemListBindings(itemList, bindingData);
                    break;
            }
        }
    }

    private void ResolveItemListBindings(IList list, IReadOnlyDictionary<string, object?> bindingData)
    {
        foreach (var item in list)
        {
            if (item is null || item.GetType().IsPrimitive || item is string)
            {
                continue;
            }

            foreach (var property in item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.PropertyType != typeof(string)
                    || !property.CanRead
                    || !property.CanWrite
                    || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (property.GetValue(item) is string value && !string.IsNullOrEmpty(value))
                {
                    property.SetValue(item, _bindingEvaluator.Evaluate(value, bindingData));
                }
            }
        }
    }

    private void ResolveListBindings(IList<string> list, IReadOnlyDictionary<string, object?> bindingData)
    {
        // ICollection<T>.IsReadOnly is true for arrays (fixed-size) even though element assignment
        // works; the non-generic IList.IsReadOnly correctly returns false for arrays and true for
        // ReadOnlyCollection<string>, ImmutableList<string>, etc.
        if (list is IList nonGenericList && nonGenericList.IsReadOnly)
        {
            return;
        }

        for (var i = 0; i < list.Count; i++)
        {
            if (!string.IsNullOrEmpty(list[i]))
            {
                list[i] = _bindingEvaluator.Evaluate(list[i], bindingData);
            }
        }
    }
}
