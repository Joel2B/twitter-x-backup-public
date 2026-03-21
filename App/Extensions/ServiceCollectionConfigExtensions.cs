using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class ServiceCollectionConfigExtensions
{
    public static Models.Config.App GetAppConfig(this IServiceCollection services)
    {
        ServiceDescriptor? descriptor = services.LastOrDefault(o =>
            o.ServiceType == typeof(Models.Config.App)
        );

        if (descriptor?.ImplementationInstance is Models.Config.App config)
            return config;

        throw new InvalidOperationException(
            "Models.Config.App is not registered as an implementation instance."
        );
    }
}
