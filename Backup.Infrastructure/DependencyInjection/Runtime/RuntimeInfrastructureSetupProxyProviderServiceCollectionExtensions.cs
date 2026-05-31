using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Proxy.Abstractions.Core;
using Backup.Infrastructure.Proxy.Services;
using Backup.Application.Proxy;
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
        services.AddScoped<IProxyHttpClientHeaderPolicyService, ProxyHttpClientHeaderPolicyService>();
        services.AddScoped<IProxyHttpClientFactoryPolicyService, ProxyHttpClientFactoryPolicyService>();
        services.AddScoped<IProxyConnectionWindowPolicyService, ProxyConnectionWindowPolicyService>();
        services.AddScoped<IProxyKeyPolicyService, ProxyKeyPolicyService>();
        services.AddScoped<ProxyProvider>();
        services.AddScoped<IProxyProvider>(sp => sp.GetRequiredService<ProxyProvider>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<ProxyProvider>());
        return services;
    }
}
