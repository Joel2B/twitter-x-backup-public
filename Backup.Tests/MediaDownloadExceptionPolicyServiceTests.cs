using Backup.Application.Media;

namespace Backup.Tests;

public class MediaDownloadExceptionPolicyServiceTests
{
    private readonly IMediaDownloadExceptionPolicyService _sut =
        new MediaDownloadExceptionPolicyService();

    [Fact]
    public void ShouldRetryWithNextProxy_ReturnsTrue_ForTaskCanceledException()
    {
        Assert.True(_sut.ShouldRetryWithNextProxy(new TaskCanceledException("timeout")));
    }

    [Fact]
    public void ShouldRetryWithNextProxy_ReturnsTrue_ForHttpRequestException()
    {
        Assert.True(_sut.ShouldRetryWithNextProxy(new HttpRequestException("network")));
    }

    [Fact]
    public void ShouldRetryWithNextProxy_ReturnsFalse_ForOtherExceptions()
    {
        Assert.False(_sut.ShouldRetryWithNextProxy(new InvalidOperationException("boom")));
    }
}
