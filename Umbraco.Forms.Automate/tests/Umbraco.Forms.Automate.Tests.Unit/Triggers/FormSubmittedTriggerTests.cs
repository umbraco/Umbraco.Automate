using Microsoft.Extensions.Options;
using Umbraco.Automate.Core.Configuration;
using Umbraco.Automate.Core.Settings;
using Umbraco.Automate.Core.Triggers;
using Umbraco.Cms.Core.Events;
using Umbraco.Forms.Automate.Triggers;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using Umbraco.Forms.Core.Services.Notifications;
using Record = Umbraco.Forms.Core.Persistence.Dtos.Record;

namespace Umbraco.Forms.Automate.Tests.Unit.Triggers;

public class FormSubmittedTriggerTests
{
    private readonly FormSubmittedTrigger _trigger = new(
        new TriggerInfrastructure(
            Mock.Of<IEditableModelResolver>(),
            Options.Create(new DeduplicationOptions { WindowMinutes = 5 })));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoForms.formSubmitted");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Form Submitted");

    [Fact]
    public void HasSettingsType()
        => _trigger.SettingsType.ShouldBe(typeof(FormRecordTriggerSettings));

    [Fact]
    public void HasOutputType()
        => _trigger.OutputType.ShouldBe(typeof(FormRecordOutput));

    [Fact]
    public void MapEvent_ProducesEventWithCorrectOutput()
    {
        var formId = Guid.NewGuid();
        var recordId = Guid.NewGuid();

        var form = new Form { Id = formId, Name = "Contact Form" };
        var record = new Record
        {
            UniqueId = recordId,
            Form = formId,
            State = FormState.Submitted,
            Created = new DateTime(2026, 3, 27, 12, 0, 0, DateTimeKind.Utc),
            IP = "127.0.0.1",
            MemberKey = "member-123",
            Culture = "en-US",
        };

        var notification = new RecordSubmittedNotification(record, new EventMessages(), form);

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);

        var evt = events[0].ShouldBeOfType<TriggerEvent<FormRecordOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoForms.formSubmitted");
        evt.InitiatorType.ShouldBe("system");
        evt.Output.FormId.ShouldBe(formId);
        evt.Output.FormName.ShouldBe("Contact Form");
        evt.Output.RecordUniqueId.ShouldBe(recordId);
        evt.Output.State.ShouldBe("Submitted");
        evt.Output.IP.ShouldBe("127.0.0.1");
        evt.Output.MemberKey.ShouldBe("member-123");
        evt.Output.Culture.ShouldBe("en-US");
    }

    [Fact]
    public void MapEvent_SetsIdempotencyKey()
    {
        var form = new Form { Id = Guid.NewGuid(), Name = "Test" };
        var record = new Record { UniqueId = Guid.NewGuid(), Form = form.Id };

        var notification = new RecordSubmittedNotification(record, new EventMessages(), form);

        var events = _trigger.MapEvent(notification).ToList();

        events[0].IdempotencyKey.ShouldNotBeNullOrWhiteSpace();
        events[0].IdempotencyKey.ShouldContain(record.UniqueId.ToString());
    }
}
