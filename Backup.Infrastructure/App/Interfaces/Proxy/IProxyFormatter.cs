using Backup.Infrastructure.Models.Proxy;

namespace Backup.Infrastructure.Interfaces.Proxy;

public interface IProxyFormatter
{
    public List<ProxyDataConfig>? Load(List<string> lines, string protocol);
}

