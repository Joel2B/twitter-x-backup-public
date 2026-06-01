using Backup.Infrastructure.Data.Partition;
using Backup.Application.Partition;
using Backup.Application.Core;
using Backup.Application.Config;
using Backup.Application.Dump;
using Backup.Application.Dump.Ports;
using Backup.Application.Bulk;
using Backup.Application.IO;
using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Infrastructure.Core.Abstractions.Config;
using Backup.Infrastructure.Core.Abstractions.Partition;
using Backup.Infrastructure.Dump.Adapters;
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
        services.AddSingleton<IPrimarySelectionService, PrimarySelectionService>();
        services.AddSingleton<ISecondaryStoreSelectionService, SecondaryStoreSelectionService>();
        services.AddSingleton<IStorageRegistrationPolicyService, StorageRegistrationPolicyService>();
        services.AddSingleton<IDumpProgressPolicyService, DumpProgressPolicyService>();
        services.AddSingleton<IDumpIndexFilePolicyService, DumpIndexFilePolicyService>();
        services.AddSingleton<IDumpContextGuardService, DumpContextGuardService>();
        services.AddSingleton<IDumpSessionNamingPolicyService, DumpSessionNamingPolicyService>();
        services.AddSingleton<IDumpLifecycleService, DumpLifecycleService>();
        services.AddSingleton<IDumpPathService, DumpPathService>();
        services.AddSingleton<IDumpIndexLoadService, DumpIndexLoadService>();
        services.AddSingleton<IDumpIndexPostsReadPort, DumpIndexPostsReadPortAdapter>();
        services.AddSingleton<IDumpFlushPlanningService, DumpFlushPlanningService>();
        services.AddSingleton<IDumpFlushExecutionService, DumpFlushExecutionService>();
        services.AddSingleton<IDumpReplicationPlanningService, DumpReplicationPlanningService>();
        services.AddSingleton<IBulkArchiveFilePolicyService, BulkArchiveFilePolicyService>();
        services.AddSingleton<IBulkPruneExecutionService, BulkPruneExecutionService>();
        services.AddSingleton<IDataStoreGuardService, DataStoreGuardService>();
        services.AddSingleton<IApiRequestBuildService, ApiRequestBuildService>();

        return services;
    }

    public static IServiceCollection AddConfigInfrastructure(this IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(AppConfig)))
            return services;

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
        services.AddSingleton<IPartitionStateProjectionService, PartitionStateProjectionService>();
        services.AddSingleton<IPartitionResolutionService, PartitionResolutionService>();
        services.AddSingleton<IPartitionPathResolutionService, PartitionPathResolutionService>();
        services.AddSingleton<IPartitionPathProbeService, PartitionPathProbeService>();
        services.AddSingleton<IPartitionPathProbePlanningService, PartitionPathProbePlanningService>();
        services.AddSingleton<IPartitionPathProbeExecutionService, PartitionPathProbeExecutionService>();
        services.AddSingleton<LocalPartition>();
        services.AddSingleton<IPartition>(sp => sp.GetRequiredService<LocalPartition>());
        services.AddSingleton<ISetup>(sp => sp.GetRequiredService<LocalPartition>());

        return services;
    }
}
