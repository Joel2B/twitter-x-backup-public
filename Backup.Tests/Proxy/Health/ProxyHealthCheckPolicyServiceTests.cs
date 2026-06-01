using Backup.Application.Proxy;

namespace Backup.Tests;

public class ProxyHealthCheckPolicyServiceTests
{
    private readonly IProxyHealthCheckPolicyService _sut = new ProxyHealthCheckPolicyService();

    [Fact]
    public void GetHealthCheckUrl_ReturnsExpectedUrl()
    {
        string url = _sut.GetHealthCheckUrl();
        Assert.Contains("pbs.twimg.com/media/", url);
    }

    [Fact]
    public void GetHealthCheckTimeout_Returns10Seconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(10), _sut.GetHealthCheckTimeout());
    }

    [Fact]
    public void ShouldFallbackToHttp_ReturnsTrue_ForSslConnectionMessage()
    {
        Exception ex = new("The SSL connection could not be established");
        Assert.True(_sut.ShouldFallbackToHttp(ex));
    }

    [Fact]
    public void ShouldFallbackToHttp_ReturnsFalse_ForOtherMessages()
    {
        Exception ex = new("connection refused");
        Assert.False(_sut.ShouldFallbackToHttp(ex));
    }
}
