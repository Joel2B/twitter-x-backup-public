using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Services;

internal sealed class ProxyProviderRuntimeEventCoordinator(
    ILogger logger,
    AppConfig config,
    IProxyFailureStateService proxyFailureStateService,
    IProxyFailureExecutionPlanService proxyFailureExecutionPlanService,
    IProxyFailureSettingsPolicyService proxyFailureSettingsPolicyService,
    IProxyRuntimeMutationService proxyRuntimeMutationService
)
{
    private readonly ILogger _logger = logger;
    private readonly AppConfig _config = config;
    private readonly IProxyFailureStateService _proxyFailureStateService = proxyFailureStateService;
    private readonly IProxyFailureExecutionPlanService _proxyFailureExecutionPlanService =
        proxyFailureExecutionPlanService;
    private readonly IProxyFailureSettingsPolicyService _proxyFailureSettingsPolicyService =
        proxyFailureSettingsPolicyService;
    private readonly IProxyRuntimeMutationService _proxyRuntimeMutationService =
        proxyRuntimeMutationService;

    public void ExecuteNext(Action rotateClient)
    {
        ProxyFailureOutcome outcome = _proxyFailureStateService.RegisterFailure(
            BuildFailureSettings()
        );
        ProxyFailureExecutionPlan plan = _proxyFailureExecutionPlanService.BuildPlan(outcome);

        _logger.LogInformation("failure count: {value}", outcome.State.FailureCount);

        if (plan.ShouldLogAttempt)
            _logger.LogInformation("attempt {attempt}", outcome.State.AttemptCount);

        switch (plan.Action)
        {
            case ProxyFailureExecutionAction.None:
                return;
            case ProxyFailureExecutionAction.ThrowPoolExhausted:
                throw new ProxyEmptyException();
            case ProxyFailureExecutionAction.ThrowStopProcess:
                throw new ProxyException();
            case ProxyFailureExecutionAction.RotateProxy:
                rotateClient();
                return;
            default:
                throw new InvalidOperationException(
                    $"Unsupported proxy failure execution action: {plan.Action}"
                );
        }
    }

    public void Reset()
    {
        if (!_config.Proxy.Enabled)
            return;

        _proxyFailureStateService.ResetFailureCount();
    }

    public void OnUse(IReadOnlyList<ProxyData> proxies)
    {
        if (!_config.Proxy.Enabled)
            return;

        int proxyIndex = _proxyFailureStateService.GetState().ProxyIndex;
        ProxyData proxy = proxies[proxyIndex];
        ProxyUseHandlingOutcome outcome;

        lock (proxy)
        {
            outcome = _proxyRuntimeMutationService.HandleUse(
                proxy,
                _proxyFailureStateService.GetState().StopCount
            );
        }

        if (outcome.ShouldLogResetStopCount)
            _logger.LogInformation("count to stop reset");

        _proxyFailureStateService.ResetStopCount();
    }

    public void OnError(IReadOnlyList<ProxyData> proxies, Exception ex)
    {
        if (!_config.Proxy.Enabled)
            return;

        int proxyIndex = _proxyFailureStateService.GetState().ProxyIndex;
        ProxyData proxy = proxies[proxyIndex];

        lock (proxy)
        {
            ProxyErrorHandlingOutcome outcome = _proxyRuntimeMutationService.HandleError(
                proxy,
                ex,
                _config.Proxy.Threshold.ErrorsToInactive
            );

            if (!outcome.ShouldApplyRuntimeRecord)
                return;

            if (outcome.WasDisabled)
                _logger.LogInformation("proxy {proxy} disabled", proxy.Proxy.ToString());
        }
    }

    private ProxyFailureSettings BuildFailureSettings() =>
        _proxyFailureSettingsPolicyService.Create(
            _config.Downloads.Threads.Start,
            _config.Proxy.Threshold.ErrorsToStop
        );
}
