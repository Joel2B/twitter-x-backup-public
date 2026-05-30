using Backup.Infrastructure.Interfaces;
using Backup.Infrastructure.Interfaces.Proxy;
using Backup.Infrastructure.Services.Proxy;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class RuntimeInfrastructureSetupProxyProviderServiceCollectionExtensions
{
    public static IServiceCollection AddProxyProviderSetupInfrastructure(
        this IServiceCollection services
    )
    {
        services.AddScoped<ProxyProvider>();
        services.AddScoped<IProxyProvider>(sp => sp.GetRequiredService<ProxyProvider>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<ProxyProvider>());
        return services;
    }
}

