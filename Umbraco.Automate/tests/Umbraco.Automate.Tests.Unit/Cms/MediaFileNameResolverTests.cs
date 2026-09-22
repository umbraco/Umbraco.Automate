using Umbraco.Automate.Core.Cms;

namespace Umbraco.Automate.Tests.Unit.Cms;

public class MediaFileNameResolverTests
{
    [Theory]
    // The common case: the URL names the file outright.
    [InlineData("https://static.tvmaze.com/uploads/images/medium_portrait/610/1525272.jpg", "1525272.jpg")]
    // A query string must never end up in the stored name.
    [InlineData("https://example.com/photo.png?width=400&v=2", "photo.png")]
    // Percent-encoded segments are decoded so the name reads as intended.
    [InlineData("https://example.com/my%20holiday.jpg", "my holiday.jpg")]
    // A trailing slash means the last real segment names the file.
    [InlineData("https://example.com/assets/logo.svg/", "logo.svg")]
    public void Resolve_UrlNamesTheFile_UsesThatName(string url, string expected)
        => MediaFileNameResolver.Resolve(new Uri(url), "Fallback", null).ShouldBe(expected);

    [Theory]
    // A version-like segment ends in something Path.HasExtension calls an extension, but the
    // table doesn't recognise ".2", so the real one still gets appended.
    [InlineData("https://example.com/images/v1.2", "image/jpeg", "v1.2.jpeg")]
    // Same for a name whose trailing dot-segment is meaningless.
    [InlineData("https://example.com/files/report.final", "application/pdf", "report.final.pdf")]
    // A name the table does recognise is left exactly as it is.
    [InlineData("https://example.com/images/photo.png", "image/png", "photo.png")]
    public void Resolve_ExtensionIsNotRecognised_AppendsTheRealOne(string url, string contentType, string expected)
        => MediaFileNameResolver.Resolve(new Uri(url), "Fallback", contentType).ShouldBe(expected);

    [Theory]
    // Types that name their own extension. Reversing the table would answer .jpe for
    // image/jpeg; asking the type to name itself and confirming it round-trips does not.
    [InlineData("image/jpeg", "42.jpeg")]
    [InlineData("image/tiff", "42.tiff")]
    [InlineData("image/png", "42.png")]
    [InlineData("image/webp", "42.webp")]
    [InlineData("application/pdf", "42.pdf")]
    [InlineData("text/csv", "42.csv")]
    // A structuring suffix is not part of the extension.
    [InlineData("image/svg+xml", "42.svg")]
    // A type that cannot name itself is left alone rather than guessed at: text/plain has
    // dozens of registered extensions and nothing picks .txt out of them.
    [InlineData("text/plain", "42")]
    [InlineData("application/octet-stream", "42")]
    public void Resolve_NoExtension_AppendsOneFromContentType(string contentType, string expected)
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/images/42"), "Fallback", contentType)
            .ShouldBe(expected);

    [Fact]
    public void Resolve_NoExtensionAndUnknownContentType_LeavesNameAlone()
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/images/42"), "Fallback", "application/x-made-up")
            .ShouldBe("42");

    [Fact]
    public void Resolve_UrlNamesNoFile_FallsBackToMediaName()
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/"), "Sunset", "image/png")
            .ShouldBe("Sunset.png");

    [Fact]
    public void Resolve_ServerStatesAFileName_PrefersItOverTheUrl()
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/download/42"), "Fallback", "image/png", "sunset.png")
            .ShouldBe("sunset.png");

    [Fact]
    public void Resolve_StatedFileNameIsQuoted_StripsTheQuotes()
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/download/42"), "Fallback", "image/png", "\"sunset.png\"")
            .ShouldBe("sunset.png");

    [Theory]
    // A remote header is untrusted input. Only its file-name part is ever used, so a server
    // cannot steer the write outside the media folder.
    [InlineData("../../web.config", "web.config")]
    [InlineData("..\\..\\web.config", "web.config")]
    [InlineData("/etc/passwd", "passwd")]
    public void Resolve_StatedFileNameCarriesAPath_KeepsOnlyTheFileName(string stated, string expected)
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/download/42"), "Fallback", null, stated)
            .ShouldBe(expected);

    [Theory]
    // Nothing usable in the header falls through to the URL rather than producing a bad name.
    [InlineData("")]
    [InlineData("..")]
    [InlineData("/")]
    public void Resolve_StatedFileNameIsUnusable_FallsBackToTheUrl(string stated)
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/images/photo.png"), "Fallback", null, stated)
            .ShouldBe("photo.png");

    [Fact]
    public void Resolve_UrlNamesNoFileAndNoMediaName_UsesAPlaceholder()
        => MediaFileNameResolver
            .Resolve(new Uri("https://example.com/"), null, "image/png")
            .ShouldBe("file.png");
}
