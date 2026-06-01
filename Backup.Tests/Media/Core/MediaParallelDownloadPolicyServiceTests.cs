using Backup.Application.Media;

namespace Backup.Tests;

public class MediaParallelDownloadPolicyServiceTests
{
    [Fact]
    public void Create_BuildsExpectedParallelSettings()
    {
        IMediaParallelDownloadPolicyService sut = new MediaParallelDownloadPolicyService();

        Backup.Application.Media.Models.MediaParallelDownloadSettings result = sut.Create(2, 8, 4);

        Assert.Equal(2, result.MinDegreeOfParallelism);
        Assert.Equal(8, result.MaxDegreeOfParallelism);
        Assert.Equal(4, result.StartDegreeOfParallelism);
        Assert.Equal(TimeSpan.FromSeconds(5), result.TargetDuration);
        Assert.False(result.JumpToMaxOnFastAverage);
        Assert.True(result.EnableHeavyCut);
        Assert.Equal(TimeSpan.FromSeconds(30), result.HeavyThreshold);
        Assert.True(result.StrictDecreaseGate);
        Assert.True(result.EnableDebug);
    }
}
