using Backup.Application.Proxy;
using Backup.Application.Proxy.Ports;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Adapters;
using Backup.Infrastructure.Proxy.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Runtime;

public static class RuntimeInfrastructureSetupProxyProviderServiceCollectionExtensions
{
    public static IServiceCollection AddProxyProviderSetupInfrastructure(
        this IServiceCollection services
    )
    {
        services
            .AddProxyPolicies()
            .AddProxyRuntimePool()
            .AddProxyHealthChecks()
            .AddProxyFailureHandling()
            .AddProxyCandidateLoading()
            .AddProxyProviderFacade();

        return services;
    }

    private static IServiceCollection AddProxyPolicies(this IServiceCollection services)
    {
        services.AddScoped<IProxyRuntimePolicyService, ProxyRuntimePolicyService>();
        services.AddScoped<IProxyHttpClientHeaderPolicyService, ProxyHttpClientHeaderPolicyService>();
        services.AddScoped<
            IProxyHttpClientFactoryPolicyService,
            ProxyHttpClientFactoryPolicyService
        >();
        services.AddScoped<IProxyConnectionWindowPolicyService, ProxyConnectionWindowPolicyService>();
        services.AddScoped<IProxyKeyPolicyService, ProxyKeyPolicyService>();
        services.AddScoped<IProxyBatchFlushPolicyService, ProxyBatchFlushPolicyService>();
        services.AddScoped<IProxyEndpointParserService, ProxyEndpointParserService>();
        services.AddScoped<IProxyProviderTypeResolverService, ProxyProviderTypeResolverService>();
        services.AddScoped<IProxyErrorDecisionService, ProxyErrorDecisionService>();
        services.AddScoped<IProxyRuntimeRecordMergeService, ProxyRuntimeRecordMergeService>();
        services.AddScoped<IProxyRuntimeStatusTransitionService, ProxyRuntimeStatusTransitionService>();

        return services;
    }

    private static IServiceCollection AddProxyRuntimePool(this IServiceCollection services)
    {
        services.AddScoped<IProxyAcceptedCandidateFactoryService, ProxyAcceptedCandidateFactoryService>();
        services.AddScoped<IProxyCandidateMergeService, ProxyCandidateMergeService>();
        services.AddScoped<IProxyRuntimePoolSelectionService, ProxyRuntimePoolSelectionService>();
        services.AddScoped<IProxyRuntimePoolProjectionService, ProxyRuntimePoolProjectionService>();
        services.AddScoped<IProxyRuntimePoolBuilderService, ProxyRuntimePoolBuilderService>();
        services.AddScoped<
            IProxyProviderRuntimeOrchestrationService,
            ProxyProviderRuntimeOrchestrationService
        >();
        services.AddScoped<IProxySetupOrchestrationService, ProxySetupOrchestrationService>();
        services.AddScoped<IProxySetupExecutionService, ProxySetupExecutionService>();
        services.AddScoped<IProxyAcceptanceApplyOrchestrationService, ProxyAcceptanceApplyOrchestrationService>();

        return services;
    }

    private static IServiceCollection AddProxyHealthChecks(this IServiceCollection services)
    {
        services.AddScoped<IProxyHealthCheckPolicyService, ProxyHealthCheckPolicyService>();
        services.AddScoped<IProxyHealthProbeService, ProxyHealthProbeService>();
        services.AddScoped<IProxyHealthAcceptanceService, ProxyHealthAcceptanceService>();
        services.AddScoped<IProxyCheckExecutionService, ProxyCheckExecutionService>();
        services.AddScoped<IProxyHealthProbePort, ProxyHealthProbePortAdapter>();

        return services;
    }

    private static IServiceCollection AddProxyFailureHandling(this IServiceCollection services)
    {
        services.AddScoped<IProxyFailureOrchestrationService, ProxyFailureOrchestrationService>();
        services.AddScoped<IProxyFailureStateService, ProxyFailureStateService>();
        services.AddScoped<IProxyFailureExecutionPlanService, ProxyFailureExecutionPlanService>();
        services.AddScoped<IProxyFailureSettingsPolicyService, ProxyFailureSettingsPolicyService>();
        services.AddScoped<IProxyErrorHandlingOrchestrationService, ProxyErrorHandlingOrchestrationService>();
        services.AddScoped<IProxyUseHandlingOrchestrationService, ProxyUseHandlingOrchestrationService>();
        services.AddScoped<IProxyUsageTrackingService, ProxyUsageTrackingService>();
        services.AddScoped<IProxyErrorTrackingService, ProxyErrorTrackingService>();
        services.AddScoped<IProxyRuntimeMutationService, ProxyRuntimeMutationService>();

        return services;
    }

    private static IServiceCollection AddProxyCandidateLoading(this IServiceCollection services)
    {
        services.AddScoped<IProxySourceLoadService, ProxySourceLoadService>();
        services.AddScoped<IProxyCandidateLoadService, ProxyCandidateLoadService>();
        services.AddScoped<IProxyCandidateLoadExecutionService, ProxyCandidateLoadExecutionService>();
        services.AddScoped<
            IProxyProviderCandidateLoadOrchestrationService,
            ProxyProviderCandidateLoadOrchestrationService
        >();
        services.AddScoped<IProxyProviderSourceInputFactory, ProxyProviderSourceInputFactory>();
        services.AddScoped<IProxyResourceLoadPort, ProxyResourceLoadPortAdapter>();
        services.AddScoped<ProxyRuntimeRecordMapper>();

        return services;
    }

    private static IServiceCollection AddProxyProviderFacade(this IServiceCollection services)
    {
        services.AddScoped<ProxyProviderDependencies>();
        services.AddScoped<ProxyProvider>();
        services.AddScoped<IProxyProvider>(sp => sp.GetRequiredService<ProxyProvider>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<ProxyProvider>());

        return services;
    }
}
