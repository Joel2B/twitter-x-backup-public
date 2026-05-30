namespace Backup.Application.Proxy;

public sealed class ProxyRuntimePolicyService : IProxyRuntimePolicyService
{
    public bool ShouldIncludeInRuntimePool(bool isActive, int connectionCount) =>
        isActive || connectionCount > 0;

    public bool ShouldAttemptProxySwitch(int failureCount, int requiredFailures) =>
        failureCount >= requiredFailures;

    public bool ShouldRotateProxy(int rotationAttemptCount, int attemptsPerProxy) =>
        rotationAttemptCount >= attemptsPerProxy;

    public bool ShouldDisableProxy(int errorCount, int errorsToInactiveThreshold) =>
        errorCount >= errorsToInactiveThreshold;

    public bool IsStopThresholdDisabled(int errorsToStopThreshold) => errorsToStopThreshold == -1;

    public bool ShouldStopProcess(int stopCount, int errorsToStopThreshold) =>
        stopCount >= errorsToStopThreshold;
}
