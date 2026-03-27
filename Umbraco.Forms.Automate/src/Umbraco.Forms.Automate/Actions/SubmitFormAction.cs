using System.Text.Json;
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Forms.Core.Persistence.Dtos;
using Umbraco.Forms.Core.Services;

namespace Umbraco.Forms.Automate.Actions;

/// <summary>
/// Programmatically submits a form entry to an Umbraco Forms form.
/// </summary>
[Action("umbracoForms.submitForm", "Submit Form",
    Description = "Programmatically submits a form entry.",
    Group = "Forms",
    Icon = "icon-checkbox-dotted")]
public sealed class SubmitFormAction : ActionBase<SubmitFormSettings, SubmitFormOutput>
{
    private readonly IFormService _formService;
    private readonly IRecordService _recordService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmitFormAction"/> class.
    /// </summary>
    public SubmitFormAction(
        ActionInfrastructure infrastructure,
        IFormService formService,
        IRecordService recordService)
        : base(infrastructure)
    {
        _formService = formService;
        _recordService = recordService;
    }

    /// <inheritdoc />
    public override async Task<ActionResult> ExecuteAsync(ActionContext context, CancellationToken cancellationToken)
    {
        var settings = context.GetSettings<SubmitFormSettings>();

        if (string.IsNullOrWhiteSpace(settings.FormId) || !Guid.TryParse(settings.FormId, out var formId))
        {
            return ActionResult.Failed(
                new ArgumentException("A valid Form ID is required."),
                StepRunErrorCategory.Validation);
        }

        var form = _formService.Get(formId);
        if (form is null)
        {
            return ActionResult.Failed(
                new InvalidOperationException($"Form '{formId}' not found."),
                StepRunErrorCategory.Validation);
        }

        // Parse field values from JSON.
        Dictionary<string, string>? fieldValues;
        try
        {
            fieldValues = !string.IsNullOrWhiteSpace(settings.FieldValuesJson)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(settings.FieldValuesJson)
                : null;
        }
        catch (JsonException ex)
        {
            return ActionResult.Failed(
                new ArgumentException($"Invalid field values JSON: {ex.Message}", ex),
                StepRunErrorCategory.Validation);
        }

        // Build the record with field values.
        var record = new Record { Form = formId };

        if (fieldValues is not null)
        {
            var fieldLookup = form.AllFields.ToDictionary(f => f.Id);
            foreach (var (key, value) in fieldValues)
            {
                if (Guid.TryParse(key, out var fieldId) && fieldLookup.TryGetValue(fieldId, out var field))
                {
                    var recordField = new RecordField(field);
                    recordField.Values.Add(value);
                    record.RecordFields[field.Id] = recordField;
                }
            }
        }

        await _recordService.SubmitAsync(record, form);

        return Success(new SubmitFormOutput
        {
            RecordUniqueId = record.UniqueId,
            FormId = formId,
            State = record.State.ToString(),
        });
    }
}
