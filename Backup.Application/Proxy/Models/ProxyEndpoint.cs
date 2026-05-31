namespace Backup.Application.Proxy.Models;

public sealed class ProxyEndpoint
{
    public required string Ip { get; init; }

    public required string Port { get; init; }

    public required string Protocol { get; init; }
}
