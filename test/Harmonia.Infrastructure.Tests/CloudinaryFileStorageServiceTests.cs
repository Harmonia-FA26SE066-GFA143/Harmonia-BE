using Harmonia.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Harmonia.Infrastructure.Tests;

/// <summary>GetSignedUrl only builds a URL, so the extension → resource type mapping is testable offline.</summary>
public class CloudinaryFileStorageServiceTests
{
    private readonly CloudinaryFileStorageService _sut = new(
        Options.Create(new CloudinaryOptions { CloudName = "cloud", ApiKey = "key", ApiSecret = "secret" }),
        NullLogger<CloudinaryFileStorageService>.Instance);

    [Theory]
    [InlineData("harmonia/music-materials/interactive-score/abc.mxl")]
    [InlineData("harmonia/music-materials/interactive-score/abc.musicxml")]
    [InlineData("harmonia/music-materials/sample-audio/abc.mid")]
    public void GetSignedUrl_ScoreAndMidiFiles_AreRawResources(string publicId)
    {
        var url = _sut.GetSignedUrl(publicId);

        Assert.Contains("/raw/", url);
    }

    [Fact]
    public void GetSignedUrl_Audio_IsVideoResource()
    {
        Assert.Contains("/video/", _sut.GetSignedUrl("harmonia/music-materials/playback/abc.wav"));
    }
}
