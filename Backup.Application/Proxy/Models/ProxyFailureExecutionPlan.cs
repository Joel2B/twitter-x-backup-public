namespace Backup.Application.Proxy.Models;

public sealed class ProxyFailureExecutionPlan
{
    public bool ShouldLogAttempt { get; init; }
    public ProxyFailureExecutionAction Action { get; init; }
}
