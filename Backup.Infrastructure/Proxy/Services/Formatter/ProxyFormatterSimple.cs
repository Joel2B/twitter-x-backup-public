using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Models;

namespace Backup.Infrastructure.Proxy.Services.Formatter;

public class ProxyFormatterSimple : IProxyFormatter
{
    public List<ProxyDataConfig>? Load(List<string> lines, string protocol)
    {
        List<ProxyDataConfig> proxies = lines
            .Select(line =>
            {
                List<string> lines = line.Split(':').ToList();

                return new ProxyDataConfig
                {
                    Ip = lines[0],
                    Port = lines[1],
                    Protocol = protocol,
                };
            })
            .ToList();

        return proxies;
    }
}
