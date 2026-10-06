namespace Umbraco.Automate.Core.Settings;

/// <summary>
/// What shape of value a settings field holds.
/// </summary>
public enum EditableModelValueKind
{
    /// <summary>
    /// A string — the only kind that can hold a <c>${ }</c> binding in place of its value.
    /// </summary>
    String = 0,

    /// <summary>
    /// Any other single value: number, bool, Guid, DateTime, enum, or an object such as ConditionSet.
    /// </summary>
    Scalar = 1,

    /// <summary>
    /// Any collection other than string, such as <c>List&lt;string&gt;</c>, <c>string[]</c> or a list of rows.
    /// </summary>
    Collection = 2,
}
