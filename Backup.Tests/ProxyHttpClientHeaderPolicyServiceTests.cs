using Backup.Application.Proxy;

namespace Backup.Tests;

public class ProxyHttpClientHeaderPolicyServiceTests
{
    private readonly IProxyHttpClientHeaderPolicyService _sut = new ProxyHttpClientHeaderPolicyService();

    [Fact]
    public void Apply_SetsExpectedHeaders()
    {
        using HttpClient client = new();
        _sut.Apply(client.DefaultRequestHeaders);

        Assert.True(client.DefaultRequestHeaders.Contains("User-Agent"));
        Assert.True(client.DefaultRequestHeaders.Contains("Priority"));
        Assert.True(client.DefaultRequestHeaders.Contains("Sec-ch-ua"));
        Assert.True(client.DefaultRequestHeaders.Contains("Sec-gpc"));

        Assert.NotEmpty(client.DefaultRequestHeaders.Accept);
        Assert.NotEmpty(client.DefaultRequestHeaders.AcceptLanguage);
    }
}
