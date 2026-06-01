using Backup.Application.Media;

namespace Backup.Tests;

public class MediaDownloadStreamingPolicyServiceTests
{
    private readonly IMediaDownloadStreamingPolicyService _sut =
        new MediaDownloadStreamingPolicyService();

    [Fact]
    public void GetSettings_ReturnsExpectedDefaults()
    {
        Backup.Application.Media.Models.MediaDownloadStreamingSettings result = _sut.GetSettings();

        Assert.Equal(128 * 1024, result.BufferSizeBytes);
        Assert.Equal(10L * 1024 * 1024, result.ProgressThresholdBytes);
        Assert.Equal(10, result.ProgressStepPercent);
    }

    [Fact]
    public void BuildNoDataTimeoutMessage_ReturnsExpectedText()
    {
        string result = _sut.BuildNoDataTimeoutMessage(15000);
        Assert.Equal("No data received in 15000 ms.", result);
    }
}
