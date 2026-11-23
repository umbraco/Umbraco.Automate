// Story S1: docs/plans/537-create-via-editing-services/STORIES.md (#537)
using Umbraco.Automate.Core.Actions;
using Umbraco.Automate.Core.Security;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn.EditingServiceCreate;

/// <summary>
/// Create Content creates through <c>IContentEditingService.CreateAsync</c>, so every CMS create
/// rule applies, content type filters included, and maps the CMS's answer onto the step's result.
/// </summary>
public class CreateContentFollowsCmsCreateRulesTests
{
    public class GivenAnInvariantTypeUnderAParent
    {
        private readonly CreateContentHarness _harness = new();

        [Fact(Skip = "Pending")]
        public async Task CreatesThroughTheEditingService()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.EditingService.Verify(x => x.CreateAsync(It.IsAny<ContentCreateModel>(), It.IsAny<Guid>()), Times.Once);
        }

        [Fact(Skip = "Pending")]
        public async Task PassesTheContentType()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.Model.ShouldNotBeNull().ContentTypeKey.ShouldBe(_harness.ContentTypeKey);
        }

        [Fact(Skip = "Pending")]
        public async Task PassesTheParent()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.Model.ShouldNotBeNull().ParentKey.ShouldBe(_harness.ParentKey);
        }

        [Fact(Skip = "Pending")]
        public async Task NamesTheItemWithNoCulture()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.Model.ShouldNotBeNull().Variants
                .Select(v => (v.Culture, v.Segment, v.Name))
                .ShouldBe([(null, null, "New Page")]);
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

            CreateActionSpecs.Output(result, "ContentKey").ShouldBe(_harness.CreatedKey);
        }
    }

    public class GivenNoParent
    {
        private readonly CreateContentHarness _harness = new();

        [Fact(Skip = "Pending")]
        public async Task CreatesAtTheRoot()
        {
            await _harness.RunAsync(_harness.Settings(atRoot: true));

            _harness.Model.ShouldNotBeNull().ParentKey.ShouldBeNull();
        }
    }

    public class GivenACultureVariantType
    {
        private readonly CreateContentHarness _harness = new(ContentVariation.Culture);

        [Fact(Skip = "Pending")]
        public async Task NamesTheItemInTheStepsCulture()
        {
            await _harness.RunAsync(_harness.Settings(culture: "en-US"));

            _harness.Model.ShouldNotBeNull().Variants
                .Select(v => (v.Culture, v.Segment, v.Name))
                .ShouldBe([("en-US", null, "New Page")]);
        }
    }

    public class GivenTheCmsRefusesTheCreate
    {
        private readonly CreateContentHarness _harness = new();

        public GivenTheCmsRefusesTheCreate() => _harness.CreateReturns(ContentEditingOperationStatus.NotAllowed);

        [Fact(Skip = "Pending")]
        public async Task FailsTheStep()
        {
            var result = await _harness.RunAsync(_harness.Settings());

            result.Status.ShouldBe(ActionResultStatus.Failed);
        }

        [Fact(Skip = "Pending")]
        public async Task ReportsAValidationError()
        {
            var result = await _harness.RunAsync(_harness.Settings());

            result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        }

        [Fact(Skip = "Pending")]
        public async Task SaysTheTypeIsNotAllowedAtTheRoot()
        {
            var result = await _harness.RunAsync(_harness.Settings(atRoot: true));

            result.Exception.ShouldNotBeNull().Message.ShouldContain("isn't allowed at the content root");
        }
    }

    public class GivenTheCmsAnswersWithAStatus
    {
        private readonly CreateContentHarness _harness = new();

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
        public async Task ATypeTheCmsCannotFindTakesContentTypeNotFound()
        {
            _harness.CreateReturns(ContentEditingOperationStatus.ContentTypeNotFound);

            var result = await _harness.RunAsync(_harness.Settings());

            result.Outcome.ShouldBe("contentTypeNotFound");
        }

        [Fact(Skip = "Pending")]
        public async Task ACancelledSaveIsCancelled()
        {
            _harness.CreateReturns(ContentEditingOperationStatus.CancelledByNotification);

            var result = await _harness.RunAsync(_harness.Settings());

            result.ErrorCategory.ShouldBe(StepRunErrorCategory.Cancelled);
        }

        [Fact(Skip = "Pending")]
        public async Task AnUnknownCultureIsAValidationError()
        {
            _harness.CreateReturns(ContentEditingOperationStatus.InvalidCulture);

            var result = await _harness.RunAsync(_harness.Settings());

            result.ErrorCategory.ShouldBe(StepRunErrorCategory.Validation);
        }

        [Fact(Skip = "Pending")]
        public async Task AnyOtherStatusIsUnknown()
        {
            _harness.CreateReturns(ContentEditingOperationStatus.DuplicateKey);

            var result = await _harness.RunAsync(_harness.Settings());

            result.ErrorCategory.ShouldBe(StepRunErrorCategory.Unknown);
        }
    }

    public class GivenTheServiceAccountIsRefused
    {
        private readonly CreateContentHarness _harness = new();

        public GivenTheServiceAccountIsRefused()
            => _harness.Authorizer
                .Setup(a => a.AuthorizeContentAsync(_harness.ParentKey, It.IsAny<IReadOnlySet<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(AutomationAuthorizationResult.Fail("No access to the parent."));

        [Fact(Skip = "Pending")]
        public async Task CreatesNothing()
        {
            await _harness.RunAsync(_harness.Settings());

            _harness.EditingService.Verify(x => x.CreateAsync(It.IsAny<ContentCreateModel>(), It.IsAny<Guid>()), Times.Never);
        }
    }

    public class GivenATypeKeyThatDoesNotResolve
    {
        private readonly CreateContentHarness _harness = new();

        public GivenATypeKeyThatDoesNotResolve()
            => _harness.ContentTypeService.Setup(x => x.Get(_harness.ContentTypeKey)).Returns((IContentType?)null);

        [Fact(Skip = "Pending")]
        public async Task TakesContentTypeNotFound()
        {
            var result = await _harness.RunAsync(_harness.Settings());

            result.Outcome.ShouldBe("contentTypeNotFound");
        }
    }
}
