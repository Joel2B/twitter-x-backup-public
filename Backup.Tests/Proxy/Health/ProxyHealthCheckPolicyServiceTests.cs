using Backup.Application.Proxy;

namespace Backup.Tests;

public class ProxyHealthCheckPolicyServiceTests
{
    private readonly IProxyHealthCheckPolicyService _sut =
        new ProxyHealthCheckPolicyService("https://example.com/proxy-health.jpg", 12);

    [Fact]
    public void GetHealthCheckUrl_ReturnsExpectedUrl()
    {
        string url = _sut.GetHealthCheckUrl();
        Assert.Equal("https://example.com/proxy-health.jpg", url);
    }

    [Fact]
    public void GetHealthCheckTimeout_ReturnsConfiguredTimeout()
    {
        Assert.Equal(TimeSpan.FromSeconds(12), _sut.GetHealthCheckTimeout());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("file:///tmp/probe")]
    public void Constructor_RejectsInvalidUrl(string url) =>
        Assert.Throws<ArgumentException>(() => new ProxyHealthCheckPolicyService(url, 10));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_RejectsInvalidTimeout(int timeoutSeconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ProxyHealthCheckPolicyService("https://example.com/probe", timeoutSeconds)
        );

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
