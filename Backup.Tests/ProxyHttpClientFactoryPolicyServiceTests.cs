using Backup.Application.Proxy;

namespace Backup.Tests;

public class ProxyHttpClientFactoryPolicyServiceTests
{
    private readonly IProxyHttpClientFactoryPolicyService _sut = new ProxyHttpClientFactoryPolicyService();

    [Fact]
    public void CreateHandler_ConfiguresProxy_WhenUriProvided()
    {
        Uri uri = new("http://127.0.0.1:8080");
        using HttpClientHandler handler = _sut.CreateHandler(uri);

        Assert.True(handler.UseProxy);
        Assert.NotNull(handler.Proxy);
    }

    [Fact]
    public void CreateClient_UsesProvidedTimeout()
    {
        using HttpClientHandler handler = _sut.CreateHandler(null);
        using HttpClient client = _sut.CreateClient(handler, TimeSpan.FromSeconds(7));

        Assert.Equal(TimeSpan.FromSeconds(7), client.Timeout);
    }
}
