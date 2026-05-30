using Backup.Application.Media;

namespace Backup.Tests;

public class MediaDownloadProgressPolicyServiceTests
{
    private readonly IMediaDownloadProgressPolicyService _sut =
        new MediaDownloadProgressPolicyService();

    [Fact]
    public void CalculatePercent_ReturnsIntegerPercent()
    {
        int result = _sut.CalculatePercent(50, 200);
        Assert.Equal(25, result);
    }

    [Fact]
    public void ShouldEmitProgressLog_ReturnsTrue_WhenPercentReachesThreshold()
    {
        Assert.True(_sut.ShouldEmitProgressLog(30, 30));
        Assert.False(_sut.ShouldEmitProgressLog(29, 30));
    }

    [Fact]
    public void GetNextThreshold_AddsStepPercent()
    {
        int result = _sut.GetNextThreshold(40, 10);
        Assert.Equal(50, result);
    }
}
