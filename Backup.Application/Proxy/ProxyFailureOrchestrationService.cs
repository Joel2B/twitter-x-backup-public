using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyFailureOrchestrationService(
    IProxyRuntimePolicyService runtimePolicyService
) : IProxyFailureOrchestrationService
{
    private readonly IProxyRuntimePolicyService _runtimePolicyService = runtimePolicyService;

    public ProxyFailureOutcome EvaluateFailure(ProxyFailureState state, ProxyFailureSettings settings)
    {
        int failureCount = state.FailureCount + 1;
        bool shouldAttemptSwitch = _runtimePolicyService.ShouldAttemptProxySwitch(
            failureCount,
            settings.DownloadThreadStart
        );

        if (!shouldAttemptSwitch)
        {
            return new()
            {
                State = CloneWith(
                    state,
                    failureCount: failureCount,
                    attemptCount: state.AttemptCount,
                    stopCount: state.StopCount,
                    proxyIndex: state.ProxyIndex
                ),
                ShouldAttemptSwitch = false,
                ShouldRotateProxy = false,
                IsPoolExhausted = false,
                ShouldStopProcess = false,
            };
        }

        int attemptCount = state.AttemptCount + 1;
        bool shouldRotateProxy = _runtimePolicyService.ShouldRotateProxy(
            attemptCount,
            settings.AttemptsPerProxy
        );

        if (!shouldRotateProxy)
        {
            return new()
            {
                State = new ProxyFailureState
                {
                    FailureCount = 0,
                    AttemptCount = attemptCount,
                    StopCount = state.StopCount,
                    ProxyIndex = state.ProxyIndex,
                    ProxyCount = state.ProxyCount,
                },
                ShouldAttemptSwitch = true,
                ShouldRotateProxy = false,
                IsPoolExhausted = false,
                ShouldStopProcess = false,
            };
        }

        int nextProxyIndex = state.ProxyIndex + 1;
        bool isPoolExhausted = nextProxyIndex >= state.ProxyCount;
        int stopCount = state.StopCount;
        bool shouldStop = false;

        if (!_runtimePolicyService.IsStopThresholdDisabled(settings.ErrorsToStop))
        {
            stopCount++;
            shouldStop = _runtimePolicyService.ShouldStopProcess(stopCount, settings.ErrorsToStop);
        }

        return new()
        {
            State = new ProxyFailureState
            {
                FailureCount = 0,
                AttemptCount = 0,
                StopCount = stopCount,
                ProxyIndex = nextProxyIndex,
                ProxyCount = state.ProxyCount,
            },
            ShouldAttemptSwitch = true,
            ShouldRotateProxy = true,
            IsPoolExhausted = isPoolExhausted,
            ShouldStopProcess = shouldStop,
        };
    }

    public ProxyFailureState ResetFailureCount(ProxyFailureState state) =>
        CloneWith(
            state,
            failureCount: 0,
            attemptCount: state.AttemptCount,
            stopCount: state.StopCount,
            proxyIndex: state.ProxyIndex
        );

    public ProxyFailureState ResetStopCount(ProxyFailureState state) =>
        CloneWith(
            state,
            failureCount: state.FailureCount,
            attemptCount: state.AttemptCount,
            stopCount: 0,
            proxyIndex: state.ProxyIndex
        );

    private static ProxyFailureState CloneWith(
        ProxyFailureState state,
        int failureCount,
        int attemptCount,
        int stopCount,
        int proxyIndex
    ) =>
        new()
        {
            FailureCount = failureCount,
            AttemptCount = attemptCount,
            StopCount = stopCount,
            ProxyIndex = proxyIndex,
            ProxyCount = state.ProxyCount,
        };
}
