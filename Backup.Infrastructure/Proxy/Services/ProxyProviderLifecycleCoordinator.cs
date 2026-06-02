using Backup.Application.Proxy;
using Backup.Application.Proxy.Models;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Proxy;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Proxy.Adapters;
using Backup.Infrastructure.Proxy.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Services;

public sealed class ProxyProviderLifecycleCoordinator(
    ILogger<ProxyProviderLifecycleCoordinator> logger,
    AppConfig config,
    IProxyData data,
    IProxyKeyPolicyService proxyKeyPolicyService,
    IProxyHealthProbePort proxyHealthProbePort,
    IProxyCheckExecutionService proxyCheckExecutionService,
    IProxyAcceptanceApplyOrchestrationService proxyAcceptanceApplyOrchestrationService,
    IProxySetupExecutionService proxySetupExecutionService,
    IProxyFailureStateService proxyFailureStateService,
    ProxyRuntimeRecordMapper proxyRuntimeRecordMapper
)
{
    private readonly ILogger<ProxyProviderLifecycleCoordinator> _logger = logger;
    private readonly AppConfig _config = config;
    private readonly IProxyData _data = data;
    private readonly IProxyKeyPolicyService _proxyKeyPolicyService = proxyKeyPolicyService;
    private readonly IProxyHealthProbePort _proxyHealthProbePort = proxyHealthProbePort;
    private readonly IProxyCheckExecutionService _proxyCheckExecutionService =
        proxyCheckExecutionService;
    private readonly IProxyAcceptanceApplyOrchestrationService _proxyAcceptanceApplyOrchestrationService =
        proxyAcceptanceApplyOrchestrationService;
    private readonly IProxySetupExecutionService _proxySetupExecutionService =
        proxySetupExecutionService;
    private readonly IProxyFailureStateService _proxyFailureStateService = proxyFailureStateService;
    private readonly ProxyRuntimeRecordMapper _proxyRuntimeRecordMapper = proxyRuntimeRecordMapper;

    public async Task CheckAsync(
        List<ProxyData> runtimePool,
        Func<Task<IReadOnlyList<ProxyCandidate>>> loadCandidates
    )
    {
        if (!_config.Proxy.Check)
            return;

        HashSet<string> proxiesAdded = [.. runtimePool.Select(item => GetProxyKey(item.Proxy))];
        List<ProxyData> proxiesStorage = await _data.GetAll() ?? [];
        ProxyHealthAcceptanceResult acceptance = await _proxyCheckExecutionService.ExecuteAsync(
            proxiesStorage.Select(_proxyRuntimeRecordMapper.ToRuntimeRecord),
            await loadCandidates(),
            proxiesAdded,
            _proxyHealthProbePort,
            flushEvery: 10
        );

        foreach (string error in acceptance.ProbeErrors)
            _logger.LogError("Error: {error}", error);

        await _proxyAcceptanceApplyOrchestrationService.ApplyAsync(
            acceptance.AcceptedItems,
            record =>
            {
                runtimePool.Add(_proxyRuntimeRecordMapper.ToProxyData(record));
                return Task.CompletedTask;
            },
            () => _data.Save(runtimePool)
        );
    }

    public async Task<List<ProxyData>> SetupRuntimePoolAsync(
        Func<Task<IReadOnlyList<ProxyCandidate>>> loadCandidates
    )
    {
        List<ProxyData> stored = (await _data.GetAllAsDictionary() ?? []).Values.ToList();
        ProxySetupExecutionResult setup = _proxySetupExecutionService.Execute(
            stored.Select(_proxyRuntimeRecordMapper.ToRuntimeRecord),
            await loadCandidates()
        );

        List<ProxyData> runtimePool = setup
            .RuntimePool.Select(_proxyRuntimeRecordMapper.ToProxyData)
            .ToList();

        if (setup.SetupPlan.ShouldThrowPoolEmpty)
            throw new ProxyEmptyException();

        if (setup.SetupPlan.ShouldInitializeFailureState)
            _proxyFailureStateService.Initialize(runtimePool.Count);

        if (setup.SetupPlan.ShouldPersistPool)
            await _data.Save(runtimePool);

        return runtimePool;
    }

    private string GetProxyKey(ProxyDataConfig proxy) =>
        _proxyKeyPolicyService.Build(proxy.Ip, proxy.Port, proxy.Protocol);
}
