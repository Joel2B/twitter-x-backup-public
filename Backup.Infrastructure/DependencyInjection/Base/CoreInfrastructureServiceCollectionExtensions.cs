using Backup.Infrastructure.Data.Partition;
using Backup.Application.Partition;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Core.Abstractions.Config;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Services.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Base;

public static class CoreInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddCoreInfrastructure(this IServiceCollection services)
    {
        services.AddConfigInfrastructure();
        services.AddCoreLoggingInfrastructure();
        services.AddPartitionInfrastructure();

        return services;
    }

    public static IServiceCollection AddConfigInfrastructure(this IServiceCollection services)
    {
        IAppConfigStore store = new JsonAppConfigStore();
        IAppConfigService configService = new AppConfigService(store);
        AppConfigSnapshot snapshot = configService.GetSnapshot();
        AppConfig config = snapshot.Value;

        services.AddSingleton(store);
        services.AddSingleton(configService);
        services.AddSingleton(config);

        return services;
    }

    public static IServiceCollection AddCoreLoggingInfrastructure(this IServiceCollection services)
    {
        services.AddLogging();
        return services;
    }

    public static IServiceCollection AddStructuredLoggingInfrastructure(
        this IServiceCollection services
    )
    {
        services.AddStructuredSerilogInfrastructure();
        return services;
    }

    public static IServiceCollection AddPartitionInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IPartitionPolicyService, PartitionPolicyService>();
        services.AddSingleton<LocalPartition>();
        services.AddSingleton<IPartition>(sp => sp.GetRequiredService<LocalPartition>());
        services.AddSingleton<ISetup>(sp => sp.GetRequiredService<LocalPartition>());

        return services;
    }
}
