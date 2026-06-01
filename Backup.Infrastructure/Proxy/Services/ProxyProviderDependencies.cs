using Backup.Application.Core;
using Backup.Application.Proxy;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Proxy.Adapters;
using Backup.Infrastructure.Proxy.Abstractions.Data;

namespace Backup.Infrastructure.Proxy.Services;

public sealed class ProxyProviderDependencies(
    IProxyData data,
    IProxyHealthProbePort proxyHealthProbePort,
    IProxyResourceLoadPort proxyResourceLoadPort,
    IProxyHttpClientFactoryPolicyService proxyHttpClientFactoryPolicyService,
    IProxyHttpClientHeaderPolicyService proxyHttpClientHeaderPolicyService,
    IProxyKeyPolicyService proxyKeyPolicyService,
    IProxyProviderCandidateLoadOrchestrationService proxyProviderCandidateLoadOrchestrationService,
    IProxySetupExecutionService proxySetupExecutionService,
    IProxyCheckExecutionService proxyCheckExecutionService,
    ProxyRuntimeRecordMapper proxyRuntimeRecordMapper,
    IProxyAcceptanceApplyOrchestrationService proxyAcceptanceApplyOrchestrationService,
    IProxyFailureStateService proxyFailureStateService,
    IProxyFailureExecutionPlanService proxyFailureExecutionPlanService,
    IProxyFailureSettingsPolicyService proxyFailureSettingsPolicyService,
    IProxyUseHandlingOrchestrationService proxyUseHandlingOrchestrationService,
    IProxyErrorHandlingOrchestrationService proxyErrorHandlingOrchestrationService,
    IDateTimeProvider dateTimeProvider
)
{
    public IProxyData Data { get; } = data;
    public IProxyHealthProbePort ProxyHealthProbePort { get; } = proxyHealthProbePort;
    public IProxyResourceLoadPort ProxyResourceLoadPort { get; } = proxyResourceLoadPort;
    public IProxyHttpClientFactoryPolicyService ProxyHttpClientFactoryPolicyService { get; } =
        proxyHttpClientFactoryPolicyService;
    public IProxyHttpClientHeaderPolicyService ProxyHttpClientHeaderPolicyService { get; } =
        proxyHttpClientHeaderPolicyService;
    public IProxyKeyPolicyService ProxyKeyPolicyService { get; } = proxyKeyPolicyService;
    public IProxyProviderCandidateLoadOrchestrationService ProxyProviderCandidateLoadOrchestrationService { get; } =
        proxyProviderCandidateLoadOrchestrationService;
    public IProxySetupExecutionService ProxySetupExecutionService { get; } = proxySetupExecutionService;
    public IProxyCheckExecutionService ProxyCheckExecutionService { get; } = proxyCheckExecutionService;
    public ProxyRuntimeRecordMapper ProxyRuntimeRecordMapper { get; } = proxyRuntimeRecordMapper;
    public IProxyAcceptanceApplyOrchestrationService ProxyAcceptanceApplyOrchestrationService { get; } =
        proxyAcceptanceApplyOrchestrationService;
    public IProxyFailureStateService ProxyFailureStateService { get; } = proxyFailureStateService;
    public IProxyFailureExecutionPlanService ProxyFailureExecutionPlanService { get; } =
        proxyFailureExecutionPlanService;
    public IProxyFailureSettingsPolicyService ProxyFailureSettingsPolicyService { get; } =
        proxyFailureSettingsPolicyService;
    public IProxyUseHandlingOrchestrationService ProxyUseHandlingOrchestrationService { get; } =
        proxyUseHandlingOrchestrationService;
    public IProxyErrorHandlingOrchestrationService ProxyErrorHandlingOrchestrationService { get; } =
        proxyErrorHandlingOrchestrationService;
    public IDateTimeProvider DateTimeProvider { get; } = dateTimeProvider;
}
