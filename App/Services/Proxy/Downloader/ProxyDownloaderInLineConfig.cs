using Backup.App.Models.Config.Proxy;
using Backup.App.Interfaces.Proxy;

namespace Backup.App.Services.Proxy.Downloader;

public class ProxyDownloaderInLineConfig(IProxyFormatter _formatter) : IProxyDownloader
{
    private readonly IProxyFormatter _formatter = _formatter;

    public Task<List<Models.Proxy.Proxy>?> Load(Resource resource)
    {
        List<string> lines = [resource.Value];
        List<Models.Proxy.Proxy>? proxies = _formatter.Load(lines, resource.Type);

        return Task.FromResult(proxies);
    }
}
