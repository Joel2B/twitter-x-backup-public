using Backup.App.Models.Config.Proxy;

namespace Backup.App.Interfaces.Proxy;

public interface IProxyDownloader
{
    public Task<List<Models.Proxy.Proxy>?> Load(Resource resource);
}
