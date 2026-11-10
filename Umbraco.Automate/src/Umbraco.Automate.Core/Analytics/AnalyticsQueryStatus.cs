namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// The outcome of checking and answering an <see cref="AnalyticsQuery"/>.
/// </summary>
public enum AnalyticsQueryStatus
{
    /// <summary>The query was valid and answered.</summary>
    Success = 0,

    /// <summary>The time zone isn't a known IANA name.</summary>
    UnknownTimeZone = 1,

    /// <summary>The range starts after it ends.</summary>
    RangeBackwards = 2,

    /// <summary>The range is longer than 366 days.</summary>
    RangeTooLong = 3,

    /// <summary>Only one of the two comparison dates was given.</summary>
    IncompleteCompare = 4,

    /// <summary>The comparison range isn't the same length as the range.</summary>
    CompareLengthMismatch = 5,

    /// <summary>A date is outside the supported range, 2000-01-01 to 2999-12-31.</summary>
    DateOutOfRange = 6,
}
