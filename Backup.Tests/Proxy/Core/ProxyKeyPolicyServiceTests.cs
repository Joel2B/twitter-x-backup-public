using Backup.Application.Proxy;

namespace Backup.Tests;

public class ProxyKeyPolicyServiceTests
{
    private readonly IProxyKeyPolicyService _sut = new ProxyKeyPolicyService();

    [Fact]
    public void Build_ReturnsLowerCaseKey()
    {
        string key = _sut.Build("127.0.0.1", "8080", "HTTP");
        Assert.Equal("127.0.0.1:8080:http", key);
    }
}
