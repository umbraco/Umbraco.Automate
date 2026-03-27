using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Runs;
using Umbraco.Automate.Tests.Common;
using Umbraco.Forms.Automate.Actions;
using Umbraco.Forms.Core.Models;
using Umbraco.Forms.Core.Persistence.Dtos;
using Umbraco.Forms.Core.Services;
using Record = Umbraco.Forms.Core.Persistence.Dtos.Record;

namespace Umbraco.Forms.Automate.Tests.Unit.Actions;

public class SubmitFormActionTests
{
    private readonly Mock<IFormService> _formService = new();
    private readonly Mock<IRecordService> _recordService = new();

    [Fact]
    public async Task InvalidFormId_ReturnsValidationError()
    {
        var result = await ActionTestHarness.For<SubmitFormAction>()
            .WithService(_formService.Object)
            .WithService(_recordService.Object)
            .WithSettings(new SubmitFormSettings { FormId = "not-a-guid" })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task EmptyFormId_ReturnsValidationError()
    {
        var result = await ActionTestHarness.For<SubmitFormAction>()
            .WithService(_formService.Object)
            .WithService(_recordService.Object)
            .WithSettings(new SubmitFormSettings { FormId = "" })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task FormNotFound_ReturnsValidationError()
    {
        var formId = Guid.NewGuid();
        _formService.Setup(s => s.Get(formId)).Returns((Form?)null);

        var result = await ActionTestHarness.For<SubmitFormAction>()
            .WithService(_formService.Object)
            .WithService(_recordService.Object)
            .WithSettings(new SubmitFormSettings { FormId = formId.ToString() })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task InvalidJson_ReturnsValidationError()
    {
        var formId = Guid.NewGuid();
        _formService.Setup(s => s.Get(formId)).Returns(new Form { Id = formId });

        var result = await ActionTestHarness.For<SubmitFormAction>()
            .WithService(_formService.Object)
            .WithService(_recordService.Object)
            .WithSettings(new SubmitFormSettings
            {
                FormId = formId.ToString(),
                FieldValuesJson = "{ bad json }",
            })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Failed);
        result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
    }

    [Fact]
    public async Task ValidSubmission_CallsRecordServiceAndReturnsSuccess()
    {
        var formId = Guid.NewGuid();
        var fieldId = Guid.NewGuid();

        var form = new Form { Id = formId };
        var field = new Field { Id = fieldId, Alias = "name", Caption = "Name" };
        form.Pages.Add(new Page());
        form.Pages[0].FieldSets.Add(new FieldSet());
        form.Pages[0].FieldSets[0].Containers.Add(new FieldsetContainer());
        form.Pages[0].FieldSets[0].Containers[0].Fields.Add(field);

        _formService.Setup(s => s.Get(formId)).Returns(form);
        _recordService.Setup(s => s.SubmitAsync(It.IsAny<Record>(), form))
            .Returns(Task.CompletedTask);

        var fieldValuesJson = $"{{\"{fieldId}\": \"John Doe\"}}";

        var result = await ActionTestHarness.For<SubmitFormAction>()
            .WithService(_formService.Object)
            .WithService(_recordService.Object)
            .WithSettings(new SubmitFormSettings
            {
                FormId = formId.ToString(),
                FieldValuesJson = fieldValuesJson,
            })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Success);
        result.OutputData.ShouldBeOfType<SubmitFormOutput>();

        var output = (SubmitFormOutput)result.OutputData!;
        output.FormId.ShouldBe(formId);
        output.RecordUniqueId.ShouldNotBe(Guid.Empty);

        _recordService.Verify(s => s.SubmitAsync(
            It.Is<Record>(r => r.RecordFields.ContainsKey(fieldId)),
            form), Times.Once);
    }

    [Fact]
    public async Task NoFieldValues_SubmitsEmptyRecord()
    {
        var formId = Guid.NewGuid();
        var form = new Form { Id = formId };

        _formService.Setup(s => s.Get(formId)).Returns(form);
        _recordService.Setup(s => s.SubmitAsync(It.IsAny<Record>(), form))
            .Returns(Task.CompletedTask);

        var result = await ActionTestHarness.For<SubmitFormAction>()
            .WithService(_formService.Object)
            .WithService(_recordService.Object)
            .WithSettings(new SubmitFormSettings
            {
                FormId = formId.ToString(),
                FieldValuesJson = "",
            })
            .ExecuteAsync();

        result.Status.ShouldBe(ActionResultStatus.Success);

        _recordService.Verify(s => s.SubmitAsync(
            It.Is<Record>(r => r.RecordFields.Count == 0),
            form), Times.Once);
    }
}
