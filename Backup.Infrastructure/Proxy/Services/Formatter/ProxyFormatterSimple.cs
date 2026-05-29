using Backup.Infrastructure.Interfaces.Proxy;
using Backup.Infrastructure.Models.Proxy;

namespace Backup.Infrastructure.Services.Proxy.Formatter;

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


