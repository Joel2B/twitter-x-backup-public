using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyEndpointParserService
{
    IReadOnlyList<ProxyEndpoint> Parse(string format, IEnumerable<string> lines, string protocol);
}
