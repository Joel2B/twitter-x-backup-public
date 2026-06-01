namespace Backup.Application.Proxy;

public interface IProxyRuntimePolicyService
{
    bool ShouldIncludeInRuntimePool(bool isActive, int connectionCount);
    bool ShouldAttemptProxySwitch(int failureCount, int requiredFailures);
    bool ShouldRotateProxy(int rotationAttemptCount, int attemptsPerProxy);
    bool ShouldDisableProxy(int errorCount, int errorsToInactiveThreshold);
    bool IsStopThresholdDisabled(int errorsToStopThreshold);
    bool ShouldStopProcess(int stopCount, int errorsToStopThreshold);
}
