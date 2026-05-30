using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Abstractions.Data;

public interface IProxyData
{
    public Task<List<ProxyData>?> GetAll();
    public Task<Dictionary<ProxyDataConfig, ProxyData>?> GetAllAsDictionary();
    public Task Save(List<ProxyData> datas);
}
