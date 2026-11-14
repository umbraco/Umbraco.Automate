// Story S2: docs/plans/537-create-via-editing-services/STORIES.md (#537)
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn.EditingServiceCreate;

/// <summary>
/// Create Media creates through <c>IMediaEditingService</c>, so every CMS create rule applies,
/// content type filters included, and maps the CMS's answer onto the step's result.
/// </summary>
public class CreateMediaFollowsCmsCreateRulesTests
{
    public class GivenAMediaTypeUnderAFolder
    {
        private readonly CreateMediaHarness _harness = new();

        [Fact(Skip = "Pending")]
        public async Task CreatesThroughTheEditingService()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.EditingService.Verify(x => x.CreateAsync(It.IsAny<MediaCreateModel>(), It.IsAny<Guid>()), Times.Once);
        }

        [Fact(Skip = "Pending")]
        public async Task PassesTheMediaType()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.Model.ShouldNotBeNull().ContentTypeKey.ShouldBe(_harness.MediaTypeKey);
        }

        [Fact(Skip = "Pending")]
        public async Task PassesTheParent()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.Model.ShouldNotBeNull().ParentKey.ShouldBe(_harness.ParentKey);
        }

        [Fact(Skip = "Pending")]
        public async Task NamesTheItem()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.Model.ShouldNotBeNull().Variants
                .Select(v => (v.Culture, v.Segment, v.Name))
                .ShouldBe([(null, null, "Sunset")]);
        }

        [Fact(Skip = "Pending")]
        public async Task Succeeds()
        {
            var result = await _harness.RunAsync(_harness.Settings());

            result.Status.ShouldBe(ActionResultStatus.Success);
        }

        [Fact(Skip = "Pending")]
        public async Task OutputsTheNewItemsKey()
        {
            var result = await _harness.RunAsync(_harness.Settings());

            CreateActionSpecs.Output(result, "MediaKey").ShouldBe(_harness.CreatedKey);
        }
    }

    public class GivenNoParent
    {
        private readonly CreateMediaHarness _harness = new();

        [Fact(Skip = "Pending")]
        public async Task CreatesAtTheRoot()
        {
            await _harness.RunAsync(_harness.Settings(atRoot: true));

            _harness.Model.ShouldNotBeNull().ParentKey.ShouldBeNull();
        }
    }

    public class GivenTheCmsAnswersWithAStatus
    {
        private readonly CreateMediaHarness _harness = new();

        [Fact(Skip = "Pending")]
        public async Task ARefusedCreateIsAValidationError()
        {
            _harness.CreateReturns(ContentEditingOperationStatus.NotAllowed);

            var result = await _harness.RunAsync(_harness.Settings());

            result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        }

        [Fact(Skip = "Pending")]
        public async Task AMissingParentTakesParentNotFound()
        {
            _harness.CreateReturns(ContentEditingOperationStatus.ParentNotFound);

            var result = await _harness.RunAsync(_harness.Settings());

            result.Outcome.ShouldBe("parentNotFound");
        }

        [Fact(Skip = "Pending")]
        public async Task ATrashedParentTakesParentNotFound()
        {
            _harness.CreateReturns(ContentEditingOperationStatus.InTrash);

            var result = await _harness.RunAsync(_harness.Settings());

            result.Outcome.ShouldBe("parentNotFound");
        }

        [Fact(Skip = "Pending")]
        public async Task ACancelledSaveIsCancelled()
        {
            _harness.CreateReturns(ContentEditingOperationStatus.CancelledByNotification);

            var result = await _harness.RunAsync(_harness.Settings());

            result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
        }
    }

    public class GivenValidationRefusesTheCreate
    {
        private readonly CreateMediaHarness _harness = new();

        public GivenValidationRefusesTheCreate() => _harness.ValidateReturns(ContentEditingOperationStatus.NotAllowed);

        [Fact(Skip = "Pending")]
        public async Task DoesNotAttemptTheCreate()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.EditingService.Verify(x => x.CreateAsync(It.IsAny<MediaCreateModel>(), It.IsAny<Guid>()), Times.Never);
        }
    }

    public class GivenATypeKeyThatDoesNotResolve
    {
        private readonly CreateMediaHarness _harness = new();

        public GivenATypeKeyThatDoesNotResolve()
            => _harness.MediaTypeService.Setup(x => x.Get(_harness.MediaTypeKey)).Returns((IMediaType?)null);

        [Fact(Skip = "Pending")]
        public async Task TakesMediaTypeNotFound()
        {
            var result = await _harness.RunAsync(_harness.Settings());

            result.Outcome.ShouldBe("mediaTypeNotFound");
        }
    }

    public class GivenTheServiceAccountIsRefused
    {
        private readonly CreateMediaHarness _harness = new();

        public GivenTheServiceAccountIsRefused()
            => _harness.Authorizer
                .Setup(a => a.AuthorizeMediaAsync(_harness.ParentKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(AutomationAuthorizationResult.Fail("No access to the folder."));

        [Fact(Skip = "Pending")]
        public async Task CreatesNothing()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.EditingService.Verify(x => x.CreateAsync(It.IsAny<MediaCreateModel>(), It.IsAny<Guid>()), Times.Never);
        }
    }
}
