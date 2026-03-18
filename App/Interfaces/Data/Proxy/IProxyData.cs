namespace Backup.App.Interfaces.Data.Proxy;

public interface IProxyData
{
    public Task<List<Models.Proxy.Data>?> GetAll();
    public Task<Dictionary<Models.Proxy.Proxy, Models.Proxy.Data>?> GetAllAsDictionary();
    public Task Save(List<Models.Proxy.Data> datas);
}
