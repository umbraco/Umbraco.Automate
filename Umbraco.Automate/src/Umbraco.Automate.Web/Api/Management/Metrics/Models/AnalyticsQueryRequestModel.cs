using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Models;

/// <summary>
/// The range, comparison range, time zone and workspace filter shared by the analytics endpoints.
/// </summary>
public class AnalyticsQueryRequestModel
{
    /// <summary>The first day, in <see cref="TimeZone"/>.</summary>
    [BindRequired]
    [FromQuery(Name = "from")]
    public DateOnly From { get; set; }

    /// <summary>The last day, inclusive.</summary>
    [BindRequired]
    [FromQuery(Name = "to")]
    public DateOnly To { get; set; }

    /// <summary>The IANA name of the time zone the days are in, such as <c>Europe/Copenhagen</c>.</summary>
    [BindRequired]
    [FromQuery(Name = "timeZone")]
    public string TimeZone { get; set; } = string.Empty;

    /// <summary>The first day of the comparison range. Needs <see cref="CompareTo"/>.</summary>
    [FromQuery(Name = "compareFrom")]
    public DateOnly? CompareFrom { get; set; }

    /// <summary>The last day of the comparison range, inclusive. Needs <see cref="CompareFrom"/>.</summary>
    [FromQuery(Name = "compareTo")]
    public DateOnly? CompareTo { get; set; }

    /// <summary>Limits the numbers to one workspace. All accessible workspaces when missing.</summary>
    [FromQuery(Name = "workspaceId")]
    public Guid? WorkspaceId { get; set; }
}
