using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Proxy.Models;
using Backup.Infrastructure.Proxy.Services.Downloader;
using Backup.Infrastructure.Proxy.Services.Formatter;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Services;

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
