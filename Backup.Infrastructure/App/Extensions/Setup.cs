using Backup.App.Data.Media;
using Backup.App.Data.Proxy;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Proxy;
using Backup.App.Interfaces.Proxy;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Services.Proxy;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class SetupCollectionExtensions
{
    public static IServiceCollection AddSetup(this IServiceCollection services)
    {
        services.AddScoped<LocalProxyData>();
        services.AddScoped<IProxyData>(sp => sp.GetRequiredService<LocalProxyData>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<LocalProxyData>());

        services.AddScoped<ProxyProvider>();
        services.AddScoped<IProxyProvider>(sp => sp.GetRequiredService<ProxyProvider>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<ProxyProvider>());

        services.AddScoped<LocalMediaLogger>();
        services.AddScoped<IMediaLogger>(sp => sp.GetRequiredService<LocalMediaLogger>());
        services.AddScoped<ISetup>(sp => sp.GetRequiredService<LocalMediaLogger>());

        return services;
    }
}
