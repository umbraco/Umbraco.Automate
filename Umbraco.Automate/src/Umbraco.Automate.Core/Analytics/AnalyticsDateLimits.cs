namespace Umbraco.Automate.Core.Analytics;

/// <summary>
/// The earliest and latest dates an <see cref="AnalyticsQuery"/> accepts. They keep the service's date
/// arithmetic (such as adding a day to the last date) clear of the edges of the calendar.
/// </summary>
internal static class AnalyticsDateLimits
{
    public static readonly DateOnly Earliest = new(2000, 1, 1);

    public static readonly DateOnly Latest = new(2999, 12, 31);

    public static bool IsOutOfRange(DateOnly date) => date < Earliest || date > Latest;
}
