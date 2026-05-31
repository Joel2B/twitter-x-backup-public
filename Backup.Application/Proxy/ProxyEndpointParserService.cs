using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyEndpointParserService : IProxyEndpointParserService
{
    public IReadOnlyList<ProxyEndpoint> Parse(string format, IEnumerable<string> lines, string protocol) =>
        format.ToLowerInvariant() switch
        {
            "ipport" => ParseIpPort(lines, protocol),
            _ => throw new NotSupportedException($"Proxy format not supported: {format}"),
        };

    private static IReadOnlyList<ProxyEndpoint> ParseIpPort(
        IEnumerable<string> lines,
        string protocol
    )
    {
        List<ProxyEndpoint> result = [];

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
                continue;

            string[] segments = trimmed.Split(':', StringSplitOptions.TrimEntries);

            if (segments.Length != 2)
                throw new FormatException($"Invalid ip:port proxy line: '{line}'");

            result.Add(
                new ProxyEndpoint
                {
                    Ip = segments[0],
                    Port = segments[1],
                    Protocol = protocol,
                }
            );
        }

        return result;
    }
}
