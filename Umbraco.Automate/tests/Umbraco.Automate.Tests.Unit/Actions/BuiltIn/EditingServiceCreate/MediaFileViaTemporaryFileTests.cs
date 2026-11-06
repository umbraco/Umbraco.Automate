// Story S5: docs/plans/537-create-via-editing-services/STORIES.md (#537)
using System.Net;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.TemporaryFile;
using Umbraco.Cms.Core.Services.OperationStatus;

namespace Umbraco.Automate.Tests.Unit.Actions.BuiltIn.EditingServiceCreate;

/// <summary>
/// Create Media downloads its Source URL into a CMS temporary file and passes the file's key on
/// <c>umbracoFile</c>, the way the backoffice passes an upload, then always deletes the temporary file.
/// </summary>
public class MediaFileViaTemporaryFileTests
{
    public class GivenASourceUrlThatReturnsAnImage
    {
        private readonly CreateMediaHarness _harness = new();

        [Fact(Skip = "Pending")]
        public async Task PutsTheFileInATemporaryFile()
        {
            await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            _harness.TemporaryFile.ShouldNotBeNull().FileName.ShouldBe("sunset.jpg");
        }

        [Fact(Skip = "Pending")]
        public async Task PointsTheFilePropertyAtTheTemporaryFile()
        {
            await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            CreateActionSpecs.JsonField(_harness.Value(Constants.Conventions.Media.File)?.Value, "temporaryFileId")
                .ShouldBe(_harness.TemporaryFile?.Key.ToString());
        }

        [Fact(Skip = "Pending")]
        public async Task OutputsTheFileName()
        {
            var result = await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            CreateActionSpecs.Output(result, "FileName").ShouldBe("sunset.jpg");
        }

        [Fact(Skip = "Pending")]
        public async Task DeletesTheTemporaryFile()
        {
            await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            _harness.TemporaryFileService.Verify(x => x.DeleteAsync(_harness.TemporaryFile!.Key), Times.Once);
        }
    }

    public class GivenADownloadThatFails
    {
        private readonly CreateMediaHarness _harness = new CreateMediaHarness().DownloadReturns(HttpStatusCode.NotFound);

        [Fact(Skip = "Pending")]
        public async Task TakesFileDownloadFailed()
        {
            var result = await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            result.Outcome.ShouldBe("fileDownloadFailed");
        }

        [Fact(Skip = "Pending")]
        public async Task StillTriesToCreateTheItem()
        {
            await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            _harness.EditingService.Verify(x => x.CreateAsync(It.IsAny<MediaCreateModel>(), It.IsAny<Guid>()), Times.Once);
        }

        [Fact(Skip = "Pending")]
        public async Task SendsNoFile()
        {
            await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            _harness.Value(Constants.Conventions.Media.File).ShouldBeNull();
        }
    }

    public class GivenAFileTheCmsRefuses
    {
        private readonly CreateMediaHarness _harness = new();

        public GivenAFileTheCmsRefuses() => _harness.TemporaryFileRefused(TemporaryFileOperationStatus.FileExtensionNotAllowed);

        [Fact(Skip = "Pending")]
        public async Task TakesFileDownloadFailed()
        {
            var result = await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            result.Outcome.ShouldBe("fileDownloadFailed");
        }
    }

    public class GivenAMediaTypeWithoutAFileProperty
    {
        private readonly CreateMediaHarness _harness = new(withFileProperty: false);

        [Fact(Skip = "Pending")]
        public async Task DownloadsNothing()
        {
            await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            _harness.TemporaryFileService.Verify(x => x.CreateAsync(It.IsAny<CreateTemporaryFileModel>()), Times.Never);
        }
    }

    public class GivenTheCmsRefusesTheCreate
    {
        private readonly CreateMediaHarness _harness = new();

        public GivenTheCmsRefusesTheCreate() => _harness.CreateReturns(ContentEditingOperationStatus.NotAllowed);

        [Fact(Skip = "Pending")]
        public async Task StillDeletesTheTemporaryFile()
        {
            await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            _harness.TemporaryFileService.Verify(x => x.DeleteAsync(_harness.TemporaryFile!.Key), Times.Once);
        }
    }

    public class GivenAFailedDownloadAndAnEmptyMandatoryFile
    {
        private readonly CreateMediaHarness _harness = new CreateMediaHarness().DownloadReturns(HttpStatusCode.NotFound);

        public GivenAFailedDownloadAndAnEmptyMandatoryFile()
            => _harness.ValidateReturns(
                ContentEditingOperationStatus.PropertyValidationError,
                CreateActionSpecs.Error(Constants.Conventions.Media.File));

        [Fact(Skip = "Pending")]
        public async Task FileDownloadFailedWins()
        {
            var result = await _harness.RunAsync(_harness.Settings(CreateMediaHarness.ImageUrl));

            result.Outcome.ShouldBe("fileDownloadFailed");
        }
    }
}
