namespace Backup.Application.Proxy.Models;

public sealed class ProxyErrorTrackingResult
{
    public required bool IsNewMessage { get; init; }
    public required bool WasDisabled { get; init; }
}
