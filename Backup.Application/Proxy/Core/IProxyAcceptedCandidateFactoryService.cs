using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyAcceptedCandidateFactoryService
{
    ProxyAcceptedCandidate Create(ProxyCandidate candidate);
}
