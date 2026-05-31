namespace Backup.Application.Proxy.Models;

public sealed class ProxyLoadRequest
{
    public required string ProviderType { get; init; }

    public required string ProviderFormat { get; init; }

    public required string ResourceType { get; init; }

    public required string ResourceValue { get; init; }
}
