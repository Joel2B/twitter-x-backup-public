using Backup.Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

public static class CoreCollectionExtensions
{
    public static IServiceCollection AddCore(this IServiceCollection services)
    {
        // Backward-compatible wrapper while infrastructure registration is split by modules.
        return services.AddCoreInfrastructure();
    }
}
