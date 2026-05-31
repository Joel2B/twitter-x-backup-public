using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyFailureStateService(
    IProxyFailureOrchestrationService proxyFailureOrchestrationService
) : IProxyFailureStateService
{
    private readonly IProxyFailureOrchestrationService _proxyFailureOrchestrationService =
        proxyFailureOrchestrationService;

    private ProxyFailureState _state = new()
    {
        FailureCount = 0,
        AttemptCount = 0,
        StopCount = 0,
        ProxyIndex = 0,
        ProxyCount = 0,
    };

    public void Initialize(int proxyCount, int proxyIndex = 0)
    {
        if (proxyCount < 0)
            throw new ArgumentOutOfRangeException(nameof(proxyCount));

        if (proxyIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(proxyIndex));

        _state = new ProxyFailureState
        {
            FailureCount = 0,
            AttemptCount = 0,
            StopCount = 0,
            ProxyIndex = proxyIndex,
            ProxyCount = proxyCount,
        };
    }

    public ProxyFailureOutcome RegisterFailure(ProxyFailureSettings settings)
    {
        ProxyFailureOutcome outcome = _proxyFailureOrchestrationService.EvaluateFailure(_state, settings);
        _state = Clone(outcome.State);
        return outcome;
    }

    public void ResetFailureCount()
    {
        _state = _proxyFailureOrchestrationService.ResetFailureCount(_state);
    }

    public void ResetStopCount()
    {
        _state = _proxyFailureOrchestrationService.ResetStopCount(_state);
    }

    public ProxyFailureState GetState() => Clone(_state);

    private static ProxyFailureState Clone(ProxyFailureState state) =>
        new()
        {
            FailureCount = state.FailureCount,
            AttemptCount = state.AttemptCount,
            StopCount = state.StopCount,
            ProxyIndex = state.ProxyIndex,
            ProxyCount = state.ProxyCount,
        };
}
