using Backup.Application.Media;

namespace Backup.Tests;

public sealed class MediaTempPathPolicyServiceTests
{
    [Fact]
    public void BuildDownloaderTempPath_ComposesAllSegments()
    {
        MediaTempPathPolicyService sut = new();

        string result = sut.BuildDownloaderTempPath(
            partitionRootPath: @"D:\data",
            tmpPathSegments: ["tmp", "media"],
            downloaderPathSegments: ["downloader", "x"]
        );

        Assert.Equal(Path.Combine(@"D:\data", "tmp", "media", "downloader", "x"), result);
    }
}
