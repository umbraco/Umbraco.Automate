namespace Umbraco.Automate.Core.Conditions;

/// <summary>
/// Comparison operators for condition evaluation.
/// </summary>
public enum ConditionOperator
{
    /// <summary>Left equals right (case-insensitive string comparison).</summary>
    Equals,

    /// <summary>Left does not equal right.</summary>
    NotEquals,

    /// <summary>Left contains right as a substring.</summary>
    Contains,

    /// <summary>Left does not contain right as a substring.</summary>
    NotContains,

    /// <summary>Left starts with right.</summary>
    StartsWith,

    /// <summary>Left ends with right.</summary>
    EndsWith,

    /// <summary>Left is numerically greater than right.</summary>
    GreaterThan,

    /// <summary>Left is numerically less than right.</summary>
    LessThan,

    /// <summary>Left is numerically greater than or equal to right.</summary>
    GreaterThanOrEquals,

    /// <summary>Left is numerically less than or equal to right.</summary>
    LessThanOrEquals,

    /// <summary>Left is null or empty.</summary>
    IsEmpty,

    /// <summary>Left is not null and not empty.</summary>
    IsNotEmpty,
}
