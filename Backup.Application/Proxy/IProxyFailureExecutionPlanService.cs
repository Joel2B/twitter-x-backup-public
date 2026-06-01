using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public interface IProxyFailureExecutionPlanService
{
    ProxyFailureExecutionPlan BuildPlan(ProxyFailureOutcome outcome);
}
