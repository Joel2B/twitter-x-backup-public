namespace Backup.Application.Proxy.Models;

public sealed class ProxyFailureState
{
    public int FailureCount { get; init; }
    public int AttemptCount { get; init; }
    public int StopCount { get; init; }
    public int ProxyIndex { get; init; }
    public int ProxyCount { get; init; }
}
