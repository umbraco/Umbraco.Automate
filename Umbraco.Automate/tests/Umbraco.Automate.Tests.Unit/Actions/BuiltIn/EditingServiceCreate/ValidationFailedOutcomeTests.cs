// Story S3: docs/plans/537-create-via-editing-services/STORIES.md (#537)
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn.EditingServiceCreate;

/// <summary>
/// Both create actions declare a Validation failed exit and take it when the CMS reports property
/// errors on values the step supplied. Content is still saved as a draft; media is not created.
/// </summary>
public class ValidationFailedOutcomeTests
{
    public class GivenTheDeclaredOutcomes
    {
        [Fact(Skip = "Pending")]
        public void CreateContentDeclaresValidationFailed()
            => new CreateContentHarness().BuildAction().GetOutcomes().Select(o => o.Key).ShouldContain("validationFailed");

        [Fact(Skip = "Pending")]
        public void CreateMediaDeclaresValidationFailed()
            => new CreateMediaHarness().BuildAction().GetOutcomes().Select(o => o.Key).ShouldContain("validationFailed");

        [Fact(Skip = "Pending")]
        public void TheExitHasALocalisedLabel()
            => new CreateContentHarness().BuildAction().GetOutcomes()
                .Single(o => o.Key == "validationFailed").Label
                .ShouldBe("#uaOutcomes_validationFailed");
    }

    public class GivenContentWithAFailingSuppliedValue
    {
        private readonly CreateContentHarness _harness = new CreateContentHarness().WithProperty("title");

        public GivenContentWithAFailingSuppliedValue()
            => _harness.CreateReturns(ContentEditingOperationStatus.PropertyValidationError, CreateActionSpecs.Error("title"));

        [Fact(Skip = "Pending")]
        public async Task TakesValidationFailed()
        {
            var result = await _harness.RunAsync(_harness.Settings("""{"title": "Hello"}"""));

            result.Outcome.ShouldBe("validationFailed");
        }

        [Fact(Skip = "Pending")]
        public async Task OutputsTheSavedItemsKey()
        {
            var result = await _harness.RunAsync(_harness.Settings("""{"title": "Hello"}"""));

            CreateActionSpecs.Output(result, "ContentKey").ShouldBe(_harness.CreatedKey);
        }

        [Fact(Skip = "Pending")]
        public async Task OutputsTheFailingAliases()
        {
            var result = await _harness.RunAsync(_harness.Settings("""{"title": "Hello"}"""));

            (CreateActionSpecs.Output(result, "InvalidProperties") as IEnumerable<string>).ShouldBe(["title"]);
        }
    }

    public class GivenContentThatPassesValidation
    {
        private readonly CreateContentHarness _harness = new CreateContentHarness().WithProperty("title");

        [Fact(Skip = "Pending")]
        public async Task OutputsNoFailingAliases()
        {
            var result = await _harness.RunAsync(_harness.Settings("""{"title": "Hello"}"""));

            (CreateActionSpecs.Output(result, "InvalidProperties") as IEnumerable<string>).ShouldBeEmpty();
        }
    }

    public class GivenMediaWithAFailingValue
    {
        private readonly CreateMediaHarness _harness = new CreateMediaHarness().WithProperty("altText");

        public GivenMediaWithAFailingValue()
            => _harness.ValidateReturns(ContentEditingOperationStatus.PropertyValidationError, CreateActionSpecs.Error("altText"));

        [Fact(Skip = "Pending")]
        public async Task TakesValidationFailed()
        {
            var result = await _harness.RunAsync(_harness.Settings(propertiesJson: """{"altText": "x"}"""));

            result.Outcome.ShouldBe("validationFailed");
        }

        [Fact(Skip = "Pending")]
        public async Task CreatesNothing()
        {
            await _harness.RunAsync(_harness.Settings(propertiesJson: """{"altText": "x"}"""));

            _harness.EditingService.Verify(x => x.CreateAsync(It.IsAny<MediaCreateModel>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact(Skip = "Pending")]
        public async Task OutputsAnEmptyMediaKey()
        {
            var result = await _harness.RunAsync(_harness.Settings(propertiesJson: """{"altText": "x"}"""));

            CreateActionSpecs.Output(result, "MediaKey").ShouldBe(Guid.Empty);
        }

        [Fact(Skip = "Pending")]
        public async Task OutputsTheFailingAliases()
        {
            var result = await _harness.RunAsync(_harness.Settings(propertiesJson: """{"altText": "x"}"""));

            (CreateActionSpecs.Output(result, "InvalidProperties") as IEnumerable<string>).ShouldBe(["altText"]);
        }
    }

    public class GivenAnErrorOnlyInAnotherCulture
    {
        private readonly CreateContentHarness _harness =
            new CreateContentHarness(ContentVariation.Culture).WithProperty("title", variations: ContentVariation.Culture);

        public GivenAnErrorOnlyInAnotherCulture()
            => _harness.CreateReturns(ContentEditingOperationStatus.PropertyValidationError, CreateActionSpecs.Error("title", "da-DK"));

        [Fact(Skip = "Pending")]
        public async Task DoesNotTakeValidationFailed()
        {
            var result = await _harness.RunAsync(_harness.Settings("""{"title": "Hello"}""", culture: "en-US"));

            result.Outcome.ShouldNotBe("validationFailed");
        }
    }

    public class GivenAMandatoryPropertyTheStepLeftOut
    {
        private readonly CreateContentHarness _harness = new CreateContentHarness().WithProperty("title").WithProperty("summary");

        public GivenAMandatoryPropertyTheStepLeftOut()
            => _harness.CreateReturns(ContentEditingOperationStatus.PropertyValidationError, CreateActionSpecs.Error("summary"));

        [Fact(Skip = "Pending")]
        public async Task DoesNotTakeValidationFailed()
        {
            var result = await _harness.RunAsync(_harness.Settings("""{"title": "Hello"}"""));

            result.Outcome.ShouldNotBe("validationFailed");
        }
    }
}
