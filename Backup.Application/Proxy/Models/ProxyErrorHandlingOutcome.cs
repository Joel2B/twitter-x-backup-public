namespace Backup.Application.Proxy.Models;

public sealed class ProxyErrorHandlingOutcome
{
    public bool ShouldApplyRuntimeRecord { get; init; }
    public bool WasDisabled { get; init; }
    public DateTime? DisabledAt { get; init; }
}
