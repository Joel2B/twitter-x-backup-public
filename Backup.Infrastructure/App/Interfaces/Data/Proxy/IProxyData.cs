using Backup.Infrastructure.Models.Proxy;

namespace Backup.Infrastructure.Interfaces.Data.Proxy;

public interface IProxyData
{
    public Task<List<ProxyData>?> GetAll();
    public Task<Dictionary<ProxyDataConfig, ProxyData>?> GetAllAsDictionary();
    public Task Save(List<ProxyData> datas);
}

