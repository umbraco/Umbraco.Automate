using Umbraco.Automate.Core.Triggers;
using Umbraco.Forms.Core.Services.Notifications;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Fires when a form entry is approved in Umbraco Forms.
/// Produces one <see cref="TriggerEvent"/> per approved record.
/// </summary>
[Trigger("umbracoForms.formEntryApproved", "Form Entry Approved",
    Description = "Fires when a form entry is approved.",
    Group = "Forms",
    Icon = "icon-check")]
public sealed class FormEntryApprovedTrigger
    : NotificationTriggerBase<FormRecordTriggerSettings, FormRecordOutput, RecordApprovedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormEntryApprovedTrigger"/> class.
    /// </summary>
    public FormEntryApprovedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(RecordApprovedNotification notification)
    {
        var record = notification.Record;
        var form = notification.Form;

        yield return new TriggerEvent<FormRecordOutput>
        {
            TriggerAlias = Alias,
            InitiatorType = "system",
            IdempotencyKey = GenerateIdempotencyKey(record.UniqueId),
            Output = new FormRecordOutput
            {
                FormId = form.Id,
                FormName = form.Name,
                RecordUniqueId = record.UniqueId,
                State = record.State.ToString(),
                Created = record.Created,
                IP = record.IP,
                MemberKey = record.MemberKey,
                Culture = record.Culture,
                RecordFieldsJson = record.GenerateRecordDataAsJson(),
            },
        };
    }
}
