using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyFailureExecutionPlanService : IProxyFailureExecutionPlanService
{
    public ProxyFailureExecutionPlan BuildPlan(ProxyFailureOutcome outcome)
    {
        if (!outcome.ShouldAttemptSwitch)
        {
            return new ProxyFailureExecutionPlan
            {
                ShouldLogAttempt = false,
                Action = ProxyFailureExecutionAction.None,
            };
        }

        if (!outcome.ShouldRotateProxy)
        {
            return new ProxyFailureExecutionPlan
            {
                ShouldLogAttempt = true,
                Action = ProxyFailureExecutionAction.None,
            };
        }

        if (outcome.IsPoolExhausted)
        {
            return new ProxyFailureExecutionPlan
            {
                ShouldLogAttempt = true,
                Action = ProxyFailureExecutionAction.ThrowPoolExhausted,
            };
        }

        if (outcome.ShouldStopProcess)
        {
            return new ProxyFailureExecutionPlan
            {
                ShouldLogAttempt = true,
                Action = ProxyFailureExecutionAction.ThrowStopProcess,
            };
        }

        return new ProxyFailureExecutionPlan
        {
            ShouldLogAttempt = true,
            Action = ProxyFailureExecutionAction.RotateProxy,
        };
    }
}
