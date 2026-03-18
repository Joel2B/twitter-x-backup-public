using Backup.App.Models.Config.Proxy;
using Backup.App.Interfaces.Proxy;
using Backup.App.Services.Proxy.Downloader;
using Backup.App.Services.Proxy.Formatter;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Proxy;

public class ProxyLoader(ILogger _logger, Models.Config.App _config)
{
    private readonly ILogger _logger = _logger;
    private readonly Models.Config.App _config = _config;
    private readonly List<Models.Proxy.Proxy> _proxies = [];

    public async Task<List<Models.Proxy.Proxy>> Load()
    {
        foreach (Provider provider in _config.Proxy.Providers)
        {
            IProxyFormatter formatter = ProxyFormatter.Create(provider.Format);

            foreach (Resource resource in provider.Resources)
            {
                IProxyDownloader downloader = new ProxyDownloader(_logger, formatter).Create(
                    provider.Type
                );

                List<Models.Proxy.Proxy>? proxies = await downloader.Load(resource);

                if (proxies is not null)
                    _proxies.AddRange(proxies);
            }
        }

        return _proxies;
    }
}
