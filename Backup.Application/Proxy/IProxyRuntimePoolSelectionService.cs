using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyRuntimePoolSelectionService
{
    IReadOnlySet<string> SelectKeys(IEnumerable<ProxyRuntimePoolCandidate> candidates);
}
