using Backup.Application.Media;
using Backup.Application.Media.Models;

namespace Backup.Tests;

public class MediaDownloadQueueBuilderServiceTests
{
    [Fact]
    public void Build_PrioritizesNonVideoBeforeVideo()
    {
        MediaDownloadQueueBuilderService sut = new(new MediaDownloadPathPriorityPolicyService());
        MediaDownloadQueueItem[] input =
        [
            new() { DownloadId = "1", Url = "u1", Path = "video.mp4" },
            new() { DownloadId = "1", Url = "u2", Path = "photo.jpg" },
        ];

        IReadOnlyList<MediaDownloadQueueItem> result = sut.Build(input, -1);

        Assert.Equal(2, result.Count);
        Assert.Equal("photo.jpg", result[0].Path);
        Assert.Equal("video.mp4", result[1].Path);
    }

    [Fact]
    public void Build_RespectsMaxCount_WhenNonNegative()
    {
        MediaDownloadQueueBuilderService sut = new(new MediaDownloadPathPriorityPolicyService());
        MediaDownloadQueueItem[] input =
        [
            new() { DownloadId = "1", Url = "u1", Path = "photo1.jpg" },
            new() { DownloadId = "1", Url = "u2", Path = "photo2.jpg" },
        ];

        IReadOnlyList<MediaDownloadQueueItem> result = sut.Build(input, 1);

        Assert.Single(result);
        Assert.Equal("photo1.jpg", result[0].Path);
    }
}
