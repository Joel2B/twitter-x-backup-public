namespace Backup.Application.Proxy.Models;

public sealed class ProxyFailureOutcome
{
    public required ProxyFailureState State { get; init; }
    public bool ShouldAttemptSwitch { get; init; }
    public bool ShouldRotateProxy { get; init; }
    public bool IsPoolExhausted { get; init; }
    public bool ShouldStopProcess { get; init; }
}
