namespace Backup.Application.Proxy.Models;

public sealed class ProxyHealthAcceptanceItem
{
    public required ProxyRuntimeRecord Record { get; init; }
    public bool ShouldFlush { get; init; }
}
