using Backup.Application.Media;

namespace Backup.Tests;

public class MediaVideoVariantPolicyServiceTests
{
    private readonly MediaVideoVariantPolicyService _sut = new();

    [Fact]
    public void GetFormatType_MapsKnownTypes()
    {
        Assert.Equal("m3u8", _sut.GetFormatType("application/x-mpegURL"));
        Assert.Equal("mp4", _sut.GetFormatType("video/mp4"));
        Assert.Null(_sut.GetFormatType("video/unknown"));
    }

    [Fact]
    public void GetResolution_ReturnsMaster_ForM3u8()
    {
        string? resolution = _sut.GetResolution("m3u8", "https://video.twimg.com/path/master.m3u8");

        Assert.Equal("master", resolution);
    }

    [Fact]
    public void GetResolution_ExtractsFromMp4Url()
    {
        string? resolution = _sut.GetResolution(
            "mp4",
            "https://video.twimg.com/ext_tw_video/abc/pu/vid/720x1280/xyz.mp4"
        );

        Assert.Equal("720x1280", resolution);
    }
}
