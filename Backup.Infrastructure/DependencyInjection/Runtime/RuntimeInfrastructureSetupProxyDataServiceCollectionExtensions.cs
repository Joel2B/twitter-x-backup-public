using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Proxy.Abstractions.Data;
using Backup.Infrastructure.Proxy.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Runtime;

public static class RuntimeInfrastructureSetupProxyDataServiceCollectionExtensions
{
    public static IServiceCollection AddProxyDataSetupInfrastructure(
        this IServiceCollection services
    )
    {
        services.AddScoped<LocalProxyData>();
        services.AddScoped<IProxyData>(sp => sp.GetRequiredService<LocalProxyData>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<LocalProxyData>());
        return services;
    }
}
