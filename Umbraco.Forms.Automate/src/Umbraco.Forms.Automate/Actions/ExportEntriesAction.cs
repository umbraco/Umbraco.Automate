using System.Text.Json;
using System.Text.Json.Nodes;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Forms.Core.Services;

namespace Umbraco.Forms.Automate.Actions;

/// <summary>
/// Exports form entries as a JSON array.
/// </summary>
[Action("umbracoForms.exportEntries", "Export Form Entries",
    Description = "Exports form entries as JSON.",
    Group = "Forms",
    Icon = "icon-list")]
public sealed class ExportEntriesAction : ActionBase<ExportEntriesSettings, ExportEntriesOutput>
{
    private readonly IRecordReaderService _recordReaderService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportEntriesAction"/> class.
    /// </summary>
    public ExportEntriesAction(
        ActionInfrastructure infrastructure,
        IRecordReaderService recordReaderService)
        : base(infrastructure)
    {
        _recordReaderService = recordReaderService;
    }

    /// <inheritdoc />
    public override Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<ExportEntriesSettings>();

        if (string.IsNullOrWhiteSpace(settings.FormId) || !Guid.TryParse(settings.FormId, out var formId))
        {
            return Task.FromResult(ActionResult.Failed(
                new ArgumentException("A valid Form ID is required."),
                StepRunErrorCategory.Validation));
        }

        var pageSize = Math.Clamp(settings.PageSize > 0 ? settings.PageSize : 100, 1, 500);

        var result = settings.ApprovedOnly
            ? _recordReaderService.GetApprovedRecordsFromForm(formId, 1, pageSize)
            : _recordReaderService.GetRecordsFromForm(formId, 1, pageSize);

        // Build a JSON array directly, embedding the pre-serialized field data
        // from GenerateRecordDataAsJson() without a deserialize-then-reserialize cycle.
        var entriesArray = new JsonArray();
        foreach (var r in result.Items)
        {
            var entry = new JsonObject
            {
                ["uniqueId"] = r.UniqueId.ToString(),
                ["created"] = r.Created,
                ["updated"] = r.Updated,
                ["state"] = r.State.ToString(),
                ["ip"] = r.IP,
                ["memberKey"] = r.MemberKey,
                ["culture"] = r.Culture,
                ["fields"] = JsonNode.Parse(r.GenerateRecordDataAsJson()),
            };
            entriesArray.Add(entry);
        }

        return Task.FromResult<ActionResult>(Success(new ExportEntriesOutput
        {
            FormId = formId,
            TotalCount = result.Total,
            ExportedCount = entriesArray.Count,
            EntriesJson = entriesArray.ToJsonString(),
        }));
    }
}
