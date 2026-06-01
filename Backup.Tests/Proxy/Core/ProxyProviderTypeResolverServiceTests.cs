using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;

namespace Backup.Tests;

public class ProxyProviderTypeResolverServiceTests
{
    [Theory]
    [InlineData("http", ProxyProviderType.Http)]
    [InlineData("HTTP", ProxyProviderType.Http)]
    [InlineData("config", ProxyProviderType.Config)]
    public void Resolve_ReturnsExpectedType(string input, ProxyProviderType expected)
    {
        ProxyProviderTypeResolverService sut = new();

        ProxyProviderType result = sut.Resolve(input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Resolve_Throws_WhenTypeUnsupported()
    {
        ProxyProviderTypeResolverService sut = new();

        Assert.Throws<NotSupportedException>(() => sut.Resolve("socks5"));
    }
}
