using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class AppCollectionExtensions
{
    public static IServiceCollection AddApp(this IServiceCollection services)
    {
        services.AddScoped<App>();
        return services;
    }
}
