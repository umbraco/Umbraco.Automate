using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Tests.Common;
using Umbraco.Cms.Core.Models;
using Umbraco.Forms.Automate.Actions;
using Umbraco.Forms.Core.Enums;
using Umbraco.Forms.Core.Persistence.Dtos;
using Umbraco.Forms.Core.Services;
using Record = Umbraco.Forms.Core.Persistence.Dtos.Record;

namespace Umbraco.Forms.Automate.Tests.Unit.Actions;

public class ExportEntriesActionTests
{
    private readonly Mock<IRecordReaderService> _readerService = new();

    [Fact]
    public async Task InvalidFormId_ReturnsValidationError()
    {
        var result = await ActionTestHarness.For<ExportEntriesAction>()
            .WithService(_readerService.Object)
            .WithSettings(new ExportEntriesSettings { FormId = "not-a-guid" })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task EmptyResults_ReturnsEmptyJson()
    {
        var formId = Guid.NewGuid();
        _readerService.Setup(s => s.GetRecordsFromForm(formId, 1, 100))
            .Returns(new PagedModel<Record>(0, Enumerable.Empty<Record>()));

        var result = await ActionTestHarness.For<ExportEntriesAction>()
            .WithService(_readerService.Object)
            .WithSettings(new ExportEntriesSettings { FormId = formId.ToString() })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Success);

        var output = result.OutputData.ShouldBeOfType<ExportEntriesOutput>();
        output.FormId.ShouldBe(formId);
        output.TotalCount.ShouldBe(0);
        output.ExportedCount.ShouldBe(0);
        output.EntriesJson.ShouldBe("[]");
    }

    [Fact]
    public async Task WithRecords_ReturnsJsonArray()
    {
        var formId = Guid.NewGuid();
        var record = new Record
        {
            UniqueId = Guid.NewGuid(),
            Form = formId,
            State = FormState.Submitted,
            Created = new DateTime(2026, 3, 27, 12, 0, 0, DateTimeKind.Utc),
            IP = "10.0.0.1",
        };

        _readerService.Setup(s => s.GetRecordsFromForm(formId, 1, 100))
            .Returns(new PagedModel<Record>(1, new[] { record }));

        var result = await ActionTestHarness.For<ExportEntriesAction>()
            .WithService(_readerService.Object)
            .WithSettings(new ExportEntriesSettings { FormId = formId.ToString() })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Success);

        var output = result.OutputData.ShouldBeOfType<ExportEntriesOutput>();
        output.TotalCount.ShouldBe(1);
        output.ExportedCount.ShouldBe(1);
        output.EntriesJson.ShouldNotBeNullOrWhiteSpace();
        output.EntriesJson.ShouldContain(record.UniqueId.ToString());
    }

    [Fact]
    public async Task ApprovedOnly_CallsApprovedEndpoint()
    {
        var formId = Guid.NewGuid();
        _readerService.Setup(s => s.GetApprovedRecordsFromForm(formId, 1, 100))
            .Returns(new PagedModel<Record>(0, Enumerable.Empty<Record>()));

        var result = await ActionTestHarness.For<ExportEntriesAction>()
            .WithService(_readerService.Object)
            .WithSettings(new ExportEntriesSettings
            {
                FormId = formId.ToString(),
                ApprovedOnly = true,
            })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Success);
        _readerService.Verify(s => s.GetApprovedRecordsFromForm(formId, 1, 100), Times.Once);
        _readerService.Verify(s => s.GetRecordsFromForm(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task PageSize_ClampedTo500()
    {
        var formId = Guid.NewGuid();
        _readerService.Setup(s => s.GetRecordsFromForm(formId, 1, 500))
            .Returns(new PagedModel<Record>(0, Enumerable.Empty<Record>()));

        var result = await ActionTestHarness.For<ExportEntriesAction>()
            .WithService(_readerService.Object)
            .WithSettings(new ExportEntriesSettings
            {
                FormId = formId.ToString(),
                PageSize = 9999,
            })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Success);
        _readerService.Verify(s => s.GetRecordsFromForm(formId, 1, 500), Times.Once);
    }
}
