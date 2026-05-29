using Backup.App.Models.Proxy;

namespace Backup.App.Interfaces.Data.Proxy;

public interface IProxyData
{
    public Task<List<ProxyData>?> GetAll();
    public Task<Dictionary<ProxyDataConfig, ProxyData>?> GetAllAsDictionary();
    public Task Save(List<ProxyData> datas);
}
