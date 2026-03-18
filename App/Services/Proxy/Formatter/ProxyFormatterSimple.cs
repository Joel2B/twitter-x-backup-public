using Backup.App.Interfaces.Proxy;

namespace Backup.App.Services.Proxy.Formatter;

public class ProxyFormatterSimple : IProxyFormatter
{
    public List<Models.Proxy.Proxy>? Load(List<string> lines, string protocol)
    {
        List<Models.Proxy.Proxy> proxies = lines
            .Select(line =>
            {
                List<string> lines = line.Split(':').ToList();

                return new Models.Proxy.Proxy
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
