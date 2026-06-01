using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyAcceptedCandidateFactoryService : IProxyAcceptedCandidateFactoryService
{
    public ProxyAcceptedCandidate Create(ProxyCandidate candidate) =>
        new() { Candidate = candidate, InitialConnectionUses = 1 };
}
