using Backup.Application.Media;

namespace Backup.Tests;

public class MediaDownloadPolicyServiceTests
{
    private readonly IMediaDownloadPolicyService _sut = new MediaDownloadPolicyService();

    [Fact]
    public void EnsureAllowedContentLength_DoesNotThrow_WhenContentLengthIsNull()
    {
        _sut.EnsureAllowedContentLength(null, 1024, bytes => $"{bytes} B");
    }

    [Fact]
    public void EnsureAllowedContentLength_DoesNotThrow_WhenBelowLimit()
    {
        _sut.EnsureAllowedContentLength(1023, 1024, bytes => $"{bytes} B");
    }

    [Fact]
    public void EnsureAllowedContentLength_Throws_WhenEqualOrAboveLimit()
    {
        SystemException ex = Assert.Throws<SystemException>(
            () => _sut.EnsureAllowedContentLength(1024, 1024, bytes => $"{bytes} B")
        );

        Assert.Equal(">= 1024 B", ex.Message);
    }

    [Fact]
    public void ShouldUseMemoryStream_ReturnsTrue_WhenWithinThreshold()
    {
        Assert.True(_sut.ShouldUseMemoryStream(100, 100));
        Assert.False(_sut.ShouldUseMemoryStream(101, 100));
        Assert.False(_sut.ShouldUseMemoryStream(null, 100));
    }

    [Fact]
    public void ShouldReportProgress_ReturnsTrue_WhenAtOrAboveThreshold()
    {
        Assert.True(_sut.ShouldReportProgress(100, 100));
        Assert.False(_sut.ShouldReportProgress(99, 100));
        Assert.False(_sut.ShouldReportProgress(null, 100));
    }

    [Fact]
    public void ShouldLogTiming_ReturnsTrue_OnlyForSingleThreadStart()
    {
        Assert.True(_sut.ShouldLogTiming(1));
        Assert.False(_sut.ShouldLogTiming(2));
    }
}
