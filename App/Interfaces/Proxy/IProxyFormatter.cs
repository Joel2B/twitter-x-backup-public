using Backup.App.Models.Proxy;

namespace Backup.App.Interfaces.Proxy;

public interface IProxyFormatter
{
    public List<ProxyDataConfig>? Load(List<string> lines, string protocol);
}
