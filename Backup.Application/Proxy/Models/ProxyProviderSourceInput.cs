namespace Backup.Application.Proxy.Models;

public sealed class ProxyProviderSourceInput
{
    public required string ProviderType { get; init; }
    public required string ProviderFormat { get; init; }
    public IReadOnlyList<ProxyProviderSourceResourceInput> Resources { get; init; } = [];
}

public sealed class ProxyProviderSourceResourceInput
{
    public required string ResourceType { get; init; }
    public required string ResourceValue { get; init; }
}
