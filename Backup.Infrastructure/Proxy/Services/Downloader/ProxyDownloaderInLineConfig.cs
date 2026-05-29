using Backup.Infrastructure.Interfaces.Proxy;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Models.Proxy;

namespace Backup.Infrastructure.Services.Proxy.Downloader;

public class ProxyDownloaderInLineConfig(IProxyFormatter _formatter) : IProxyDownloader
{
    private readonly IProxyFormatter _formatter = _formatter;

    public Task<List<ProxyDataConfig>?> Load(Resource resource)
    {
        List<string> lines = [resource.Value];
        List<ProxyDataConfig>? proxies = _formatter.Load(lines, resource.Type);

        return Task.FromResult(proxies);
    }
}


