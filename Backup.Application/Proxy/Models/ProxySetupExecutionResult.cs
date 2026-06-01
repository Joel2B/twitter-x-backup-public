namespace Backup.Application.Proxy.Models;

public sealed class ProxySetupExecutionResult
{
    public required IReadOnlyList<ProxyRuntimeRecord> RuntimePool { get; init; }

    public required ProxySetupPlan SetupPlan { get; init; }
}
