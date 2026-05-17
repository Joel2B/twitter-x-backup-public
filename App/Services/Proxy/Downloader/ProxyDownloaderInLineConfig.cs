using Backup.App.Interfaces.Proxy;
using Backup.App.Models.Proxy;

namespace Backup.App.Services.Proxy.Downloader;

public class ProxyDownloaderInLineConfig(IProxyFormatter _formatter) : IProxyDownloader
{
    private readonly IProxyFormatter _formatter = _formatter;

    public Task<List<ProxyDataConfig>?> Load(Models.Config.Proxy.Resource resource)
    {
        List<string> lines = [resource.Value];
        List<ProxyDataConfig>? proxies = _formatter.Load(lines, resource.Type);

        return Task.FromResult(proxies);
    }
}
