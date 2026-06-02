using Backup.Application.Proxy;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Adapters;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Proxy.Services;

public sealed class ProxyProviderDependencies(
    ILogger<ProxyProvider> logger,
    AppConfig config,
    IProxyData data,
    IProxyResourceLoadPort proxyResourceLoadPort,
    IProxyProviderSourceInputFactory proxyProviderSourceInputFactory,
    IProxyProviderCandidateLoadOrchestrationService proxyProviderCandidateLoadOrchestrationService,
    IProxyFailureStateService proxyFailureStateService,
    IProxyFailureExecutionPlanService proxyFailureExecutionPlanService,
    IProxyFailureSettingsPolicyService proxyFailureSettingsPolicyService,
    IProxyRuntimeMutationService proxyRuntimeMutationService,
    IProxyClientRotationService proxyClientRotationService,
    IProxyProviderLifecycleService proxyProviderLifecycleService
)
{
    public ILogger<ProxyProvider> Logger { get; } = logger;
    public AppConfig Config { get; } = config;
    public IProxyData Data { get; } = data;
    public IProxyResourceLoadPort ProxyResourceLoadPort { get; } = proxyResourceLoadPort;
    public IProxyProviderSourceInputFactory ProxyProviderSourceInputFactory { get; } =
        proxyProviderSourceInputFactory;
    public IProxyProviderCandidateLoadOrchestrationService ProxyProviderCandidateLoadOrchestrationService { get; } =
        proxyProviderCandidateLoadOrchestrationService;
    public IProxyFailureStateService ProxyFailureStateService { get; } = proxyFailureStateService;
    public IProxyFailureExecutionPlanService ProxyFailureExecutionPlanService { get; } =
        proxyFailureExecutionPlanService;
    public IProxyFailureSettingsPolicyService ProxyFailureSettingsPolicyService { get; } =
        proxyFailureSettingsPolicyService;
    public IProxyRuntimeMutationService ProxyRuntimeMutationService { get; } =
        proxyRuntimeMutationService;
    public IProxyClientRotationService ProxyClientRotationService { get; } =
        proxyClientRotationService;
    public IProxyProviderLifecycleService ProxyProviderLifecycleService { get; } =
        proxyProviderLifecycleService;
}
