using System.ComponentModel.DataAnnotations;

namespace Umbraco.Automate.Web.Api.Management.Metrics.Models;

/// <summary>
/// The runs that started on one local day, by outcome.
/// </summary>
public sealed class AnalyticsDayResponseModel
{
    /// <summary>The local day, as <c>yyyy-MM-dd</c>.</summary>
    [Required]
    public DateOnly Date { get; set; }

    /// <summary>The completed runs.</summary>
    public int Completed { get; set; }

    /// <summary>The runs a person refused.</summary>
    public int Rejected { get; set; }

    /// <summary>The cancelled runs.</summary>
    public int Cancelled { get; set; }

    /// <summary>The failed runs.</summary>
    public int Failed { get; set; }

    /// <summary>The pending, running and suspended runs.</summary>
    public int InProgress { get; set; }
}
