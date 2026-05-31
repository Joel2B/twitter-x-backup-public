namespace Backup.Application.Proxy.Models;

public sealed class ProxyHealthAcceptanceResult
{
    public IReadOnlyList<ProxyHealthAcceptanceItem> AcceptedItems { get; init; } = [];
    public IReadOnlyList<string> ProbeErrors { get; init; } = [];
}
