using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;

namespace Backup.Tests;

public class ProxyRuntimePoolSelectionServiceTests
{
    [Fact]
    public void SelectKeys_ReturnsOnlyActiveWithConnections()
    {
        IProxyRuntimePolicyService policy = new ProxyRuntimePolicyService();
        ProxyRuntimePoolSelectionService sut = new(policy);
        ProxyRuntimePoolCandidate[] candidates =
        [
            new() { Key = "a", IsActive = true, ConnectionCount = 1 },
            new() { Key = "b", IsActive = true, ConnectionCount = 0 },
            new() { Key = "c", IsActive = false, ConnectionCount = 2 },
            new() { Key = "d", IsActive = false, ConnectionCount = 0 },
        ];

        IReadOnlySet<string> keys = sut.SelectKeys(candidates);

        Assert.Equal(3, keys.Count);
        Assert.Contains("a", keys);
        Assert.Contains("b", keys);
        Assert.Contains("c", keys);
        Assert.DoesNotContain("d", keys);
    }
}
