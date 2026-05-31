using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;

namespace Backup.Tests;

public class ProxyCandidateMergeServiceTests
{
    [Fact]
    public void MergeDistinct_PreservesOrderAndRemovesDuplicates()
    {
        ProxyCandidateMergeService sut = new();
        ProxyCandidate[] primary =
        [
            new() { Ip = "1.1.1.1", Port = "80", Protocol = "http" },
            new() { Ip = "2.2.2.2", Port = "80", Protocol = "http" },
        ];
        ProxyCandidate[] secondary =
        [
            new() { Ip = "2.2.2.2", Port = "80", Protocol = "http" },
            new() { Ip = "3.3.3.3", Port = "443", Protocol = "https" },
        ];

        IReadOnlyList<ProxyCandidate> result = sut.MergeDistinct(primary, secondary);

        Assert.Equal(3, result.Count);
        Assert.Equal("1.1.1.1", result[0].Ip);
        Assert.Equal("2.2.2.2", result[1].Ip);
        Assert.Equal("3.3.3.3", result[2].Ip);
    }
}
