using Backup.Application.Media;

namespace Backup.Tests;

public sealed class MediaStoragePathServiceTests
{
    private readonly MediaStoragePathService _sut = new(
        new MediaPathSelectionService(),
        new MediaTempPathPolicyService()
    );

    [Fact]
    public void BuildMediaRootPath_UsesFirstNonEmptyRoot()
    {
        string path = _sut.BuildMediaRootPath(["", " ", @"D:\media-root"], ["media", "data"]);

        Assert.Equal(Path.Combine(@"D:\media-root", "media", "data"), path);
    }

    [Fact]
    public void BuildMediaLogAndErrorPath_ComposesFromRoot()
    {
        string root = Path.Combine(@"D:\media-root", "media", "data");

        string logPath = _sut.BuildMediaLogPath(root, ["log"]);
        string errorPath = _sut.BuildMediaErrorPath(root, ["error"]);

        Assert.Equal(Path.Combine(root, "log"), logPath);
        Assert.Equal(Path.Combine(root, "error"), errorPath);
    }

    [Fact]
    public void BuildDownloaderTempPath_ComposesTempAndDownloaderSegments()
    {
        string path = _sut.BuildDownloaderTempPath(
            [@"E:\heavy-root"],
            ["tmp"],
            ["downloader"]
        );

        Assert.Equal(Path.Combine(@"E:\heavy-root", "tmp", "downloader"), path);
    }
}
