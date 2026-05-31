namespace Backup.Application.Proxy.Models;

public sealed class ProxyLoadProviderDefinition
{
    public required string ProviderType { get; init; }

    public required string ProviderFormat { get; init; }

    public required IReadOnlyList<ProxyLoadResourceDefinition> Resources { get; init; }
}
