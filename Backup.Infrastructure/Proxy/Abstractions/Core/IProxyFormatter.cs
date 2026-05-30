using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Abstractions.Core;

public interface IProxyFormatter
{
    public List<ProxyDataConfig>? Load(List<string> lines, string protocol);
}
