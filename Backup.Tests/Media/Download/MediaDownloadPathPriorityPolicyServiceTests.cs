using Backup.Application.Media;

namespace Backup.Tests;

public class MediaDownloadPathPriorityPolicyServiceTests
{
    private readonly IMediaDownloadPathPriorityPolicyService _sut =
        new MediaDownloadPathPriorityPolicyService();

    [Theory]
    [InlineData("a.mp4", 1)]
    [InlineData("a.webm", 1)]
    [InlineData("a.MP4", 1)]
    [InlineData("a.jpg", 0)]
    [InlineData("a", 0)]
    public void GetPriority_ReturnsExpectedValue(string path, int expected)
    {
        int result = _sut.GetPriority(path);
        Assert.Equal(expected, result);
    }
}
