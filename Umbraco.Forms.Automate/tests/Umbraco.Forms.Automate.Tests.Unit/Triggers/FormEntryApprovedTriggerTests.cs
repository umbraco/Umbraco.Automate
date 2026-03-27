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

public class FormEntryApprovedTriggerTests
{
    private readonly FormEntryApprovedTrigger _trigger = new(
        new TriggerInfrastructure(
            Mock.Of<IEditableModelResolver>(),
            Options.Create(new DeduplicationOptions { WindowMinutes = 5 })));

    [Fact]
    public void HasCorrectAlias()
        => _trigger.Alias.ShouldBe("umbracoForms.formEntryApproved");

    [Fact]
    public void HasCorrectName()
        => _trigger.Name.ShouldBe("Form Entry Approved");

    [Fact]
    public void MapEvent_ProducesEventWithApprovedState()
    {
        var form = new Form { Id = Guid.NewGuid(), Name = "Approval Form" };
        var record = new Record
        {
            UniqueId = Guid.NewGuid(),
            Form = form.Id,
            State = FormState.Approved,
        };

        var notification = new RecordApprovedNotification(record, new EventMessages(), form);

        var events = _trigger.MapEvent(notification).ToList();

        events.Count.ShouldBe(1);

        var evt = events[0].ShouldBeOfType<TriggerEvent<FormRecordOutput>>();
        evt.TriggerAlias.ShouldBe("umbracoForms.formEntryApproved");
        evt.Output.State.ShouldBe("Approved");
        evt.Output.FormName.ShouldBe("Approval Form");
    }
}
