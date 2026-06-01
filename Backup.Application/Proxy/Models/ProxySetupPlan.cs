namespace Backup.Application.Proxy.Models;

public sealed class ProxySetupPlan
{
    public bool ShouldThrowPoolEmpty { get; init; }
    public bool ShouldInitializeFailureState { get; init; }
    public bool ShouldPersistPool { get; init; }
}
