using Umbraco.Automate.Core.Triggers;
using Umbraco.Forms.Core.Services.Notifications;

namespace Umbraco.Forms.Automate.Triggers;

/// <summary>
/// Fires when a form entry is submitted in Umbraco Forms.
/// Produces one <see cref="TriggerEvent"/> per submitted record.
/// </summary>
[Trigger("umbracoForms.formSubmitted", "Form Submitted",
    Description = "Fires when a form entry is submitted.",
    Group = "Forms",
    Icon = "icon-checkbox")]
public sealed class FormSubmittedTrigger
    : NotificationTriggerBase<FormRecordTriggerSettings, FormRecordOutput, RecordSubmittedNotification>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FormSubmittedTrigger"/> class.
    /// </summary>
    public FormSubmittedTrigger(TriggerInfrastructure infrastructure) : base(infrastructure)
    {
    }

    /// <inheritdoc />
    public override IEnumerable<TriggerEvent> MapEvent(RecordSubmittedNotification notification)
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
