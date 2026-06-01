using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;

namespace Backup.Tests;

public class ProxyAcceptedCandidateFactoryServiceTests
{
    [Fact]
    public void Create_ReturnsCandidateWithDefaultInitialUses()
    {
        ProxyAcceptedCandidateFactoryService sut = new();
        ProxyCandidate candidate = new()
        {
            Ip = "1.1.1.1",
            Port = "80",
            Protocol = "http",
        };

        ProxyAcceptedCandidate result = sut.Create(candidate);

        Assert.Equal(candidate, result.Candidate);
        Assert.Equal(1, result.InitialConnectionUses);
    }
}
