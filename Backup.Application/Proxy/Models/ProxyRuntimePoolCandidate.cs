namespace Backup.Application.Proxy.Models;

public sealed class ProxyRuntimePoolCandidate
{
    public required string Key { get; init; }

    public required bool IsActive { get; init; }

    public required int ConnectionCount { get; init; }
}
