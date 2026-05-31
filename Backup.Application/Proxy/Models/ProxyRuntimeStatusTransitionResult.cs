namespace Backup.Application.Proxy.Models;

public sealed class ProxyRuntimeStatusTransitionResult
{
    public bool IsActive { get; init; }
    public DateTime? StatusDate { get; init; }
}
