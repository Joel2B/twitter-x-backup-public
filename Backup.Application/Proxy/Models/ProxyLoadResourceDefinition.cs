namespace Backup.Application.Proxy.Models;

public sealed class ProxyLoadResourceDefinition
{
    public required string ResourceType { get; init; }

    public required string ResourceValue { get; init; }
}
