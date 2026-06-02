using Backup.Application.Proxy;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Adapters;
using Backup.Infrastructure.Proxy.Abstractions.Data;

namespace Backup.Infrastructure.Proxy.Services;

public sealed class ProxyProviderDependencies(
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
