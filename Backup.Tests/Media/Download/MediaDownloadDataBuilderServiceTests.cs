using Backup.Application.Media;
using Backup.Application.Media.Models;

namespace Backup.Tests;

public class MediaDownloadDataBuilderServiceTests
{
    private readonly MediaDownloadDataBuilderService _sut = new();

    [Fact]
    public void Build_AppendsQuery_WhenIncludeQueryTrue()
    {
        MediaDownloadData data = _sut.Build(
            new MediaDownloadDataBuildInput
            {
                PostId = "p1",
                Url = "https://pbs.twimg.com/media/a.jpg",
                MediaType = "photo",
                MidPath = ["mid"],
                FormatType = "jpg",
                ResolutionType = "size",
                Name = "orig",
            }
        );

        Assert.Contains("format=jpg", data.Url, StringComparison.Ordinal);
        Assert.Contains("name=orig", data.Url, StringComparison.Ordinal);
        Assert.EndsWith(
            Path.Combine("jpg", "size", "orig.jpg"),
            data.Path,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void Build_DoesNotAppendQuery_WhenIncludeQueryFalse()
    {
        MediaDownloadData data = _sut.Build(
            new MediaDownloadDataBuildInput
            {
                PostId = "p1",
                Url = "https://pbs.twimg.com/media/a.jpg",
                MediaType = "photo",
                MidPath = [],
                FormatType = "jpg",
                ResolutionType = "size",
                Name = "orig",
                IncludeQuery = false,
            }
        );

        Assert.Equal("https://pbs.twimg.com/media/a.jpg", data.Url);
    }

    [Fact]
    public void Build_Throws_WhenUrlInvalid()
    {
        ArgumentException ex = Assert.Throws<ArgumentException>(
            () =>
                _sut.Build(
                    new MediaDownloadDataBuildInput
                    {
                        PostId = "p1",
                        Url = "not-valid-url",
                        MediaType = "photo",
                        MidPath = [],
                        FormatType = "jpg",
                        ResolutionType = "size",
                        Name = "orig",
                    }
                )
        );

        Assert.Contains("invalid absolute URL", ex.Message, StringComparison.Ordinal);
    }
}
