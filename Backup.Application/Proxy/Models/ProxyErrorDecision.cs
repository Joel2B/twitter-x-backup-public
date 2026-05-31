namespace Backup.Application.Proxy.Models;

public sealed class ProxyErrorDecision
{
    public required bool IsNewMessage { get; init; }

    public required bool ShouldDisable { get; init; }
}
