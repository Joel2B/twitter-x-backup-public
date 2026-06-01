using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Adapters;
using Backup.Infrastructure.Proxy.Services;
using Backup.Application.Proxy;
using Backup.Application.Proxy.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Runtime;

public static class RuntimeInfrastructureSetupProxyProviderServiceCollectionExtensions
{
    public static IServiceCollection AddProxyProviderSetupInfrastructure(
        this IServiceCollection services
    )
    {
        services.AddScoped<IProxyRuntimePolicyService, ProxyRuntimePolicyService>();
        services.AddScoped<IProxyHealthCheckPolicyService, ProxyHealthCheckPolicyService>();
        services.AddScoped<IProxyHealthProbeService, ProxyHealthProbeService>();
        services.AddScoped<IProxyHealthAcceptanceService, ProxyHealthAcceptanceService>();
        services.AddScoped<IProxyHttpClientHeaderPolicyService, ProxyHttpClientHeaderPolicyService>();
        services.AddScoped<IProxyHttpClientFactoryPolicyService, ProxyHttpClientFactoryPolicyService>();
        services.AddScoped<IProxyConnectionWindowPolicyService, ProxyConnectionWindowPolicyService>();
        services.AddScoped<IProxyKeyPolicyService, ProxyKeyPolicyService>();
        services.AddScoped<IProxyErrorDecisionService, ProxyErrorDecisionService>();
        services.AddScoped<IProxyAcceptedCandidateFactoryService, ProxyAcceptedCandidateFactoryService>();
        services.AddScoped<IProxyBatchFlushPolicyService, ProxyBatchFlushPolicyService>();
        services.AddScoped<IProxyCandidateMergeService, ProxyCandidateMergeService>();
        services.AddScoped<IProxyEndpointParserService, ProxyEndpointParserService>();
        services.AddScoped<IProxyProviderTypeResolverService, ProxyProviderTypeResolverService>();
        services.AddScoped<IProxyRuntimePoolSelectionService, ProxyRuntimePoolSelectionService>();
        services.AddScoped<IProxyRuntimePoolProjectionService, ProxyRuntimePoolProjectionService>();
        services.AddScoped<IProxyRuntimePoolBuilderService, ProxyRuntimePoolBuilderService>();
        services.AddScoped<IProxyProviderRuntimeOrchestrationService, ProxyProviderRuntimeOrchestrationService>();
        services.AddScoped<IProxyRuntimeRecordMergeService, ProxyRuntimeRecordMergeService>();
        services.AddScoped<IProxyRuntimeStatusTransitionService, ProxyRuntimeStatusTransitionService>();
        services.AddScoped<IProxyFailureOrchestrationService, ProxyFailureOrchestrationService>();
        services.AddScoped<IProxyFailureStateService, ProxyFailureStateService>();
        services.AddScoped<IProxyFailureExecutionPlanService, ProxyFailureExecutionPlanService>();
        services.AddScoped<IProxyErrorHandlingOrchestrationService, ProxyErrorHandlingOrchestrationService>();
        services.AddScoped<IProxyFailureSettingsPolicyService, ProxyFailureSettingsPolicyService>();
        services.AddScoped<IProxyUseHandlingOrchestrationService, ProxyUseHandlingOrchestrationService>();
        services.AddScoped<IProxyUsageTrackingService, ProxyUsageTrackingService>();
        services.AddScoped<IProxyErrorTrackingService, ProxyErrorTrackingService>();
        services.AddScoped<IProxySourceLoadService, ProxySourceLoadService>();
        services.AddScoped<IProxyCandidateLoadService, ProxyCandidateLoadService>();
        services.AddScoped<IProxyHealthProbePort, ProxyHealthProbePortAdapter>();
        services.AddScoped<IProxyRuntimeRecordMapper, ProxyRuntimeRecordMapper>();
        services.AddScoped<ProxyProvider>();
        services.AddScoped<IProxyProvider>(sp => sp.GetRequiredService<ProxyProvider>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<ProxyProvider>());
        return services;
    }
}
