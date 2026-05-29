using Backup.Infrastructure.Interfaces.Proxy;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Models.Proxy;
using Backup.Infrastructure.Services.Proxy.Downloader;
using Backup.Infrastructure.Services.Proxy.Formatter;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Services.Proxy;

public class ProxyLoader(ILogger _logger, AppConfig _config)
{
    private readonly ILogger _logger = _logger;
    private readonly AppConfig _config = _config;
    private readonly List<ProxyDataConfig> _proxies = [];

    public async Task<List<ProxyDataConfig>> Load()
    {
        foreach (Provider provider in _config.Proxy.Providers)
        {
            IProxyFormatter formatter = ProxyFormatter.Create(provider.Format);

            foreach (Resource resource in provider.Resources)
            {
                IProxyDownloader downloader = new ProxyDownloader(_logger, formatter).Create(
                    provider.Type
                );

                List<ProxyDataConfig>? proxies = await downloader.Load(resource);

                if (proxies is not null)
                    _proxies.AddRange(proxies);
            }
        }

        return _proxies;
    }
}


