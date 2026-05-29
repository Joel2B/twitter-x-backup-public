using Backup.App.Data.Bulk;
using Backup.App.Data.Media;
using Backup.App.Data.Partition;
using Backup.App.Data.Posts;
using Backup.App.Interfaces;
using Backup.App.Interfaces.Data.Bulk;
using Backup.App.Interfaces.Data.Media;
using Backup.App.Interfaces.Data.Posts;
using Backup.App.Interfaces.Partition;
using Backup.App.Interfaces.Services.Media;
using Backup.App.Models.Config;
using Backup.App.Models.Config.Data;
using Backup.App.Models.Config.Data.Backup;
using Backup.App.Models.Config.Data.Bulk;
using Backup.App.Models.Config.Data.Dump;
using Backup.App.Models.Config.Data.Media;
using Backup.App.Models.Config.Data.Posts;
using Backup.App.Services.Media;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

public static class DataInfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPostDataInfrastructure(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new()
        {
            ["local"] = typeof(LocalPostData),
            ["sqlite"] = typeof(SqlitePostData),
        };

        List<DataRegistration<StoragePost>> registrations = ResolveRegistrations(
            services,
            GetAppConfig(services).Data.Post,
            types,
            keyOffset: 0
        );

        foreach (DataRegistration<StoragePost> registration in registrations)
        {
            StoragePost storage = registration.Storage;
            string key = registration.Key;
            Type type = registration.ImplementationType;

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    (IPartition)ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage)
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    IPostDataStore instance = (IPostDataStore)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = registration.Id;
                    instance.IsDefault = storage.Default;

                    return instance;
                }
            );

            if (IsSetupType(type))
            {
                services.AddKeyedScoped(
                    key,
                    (sp, _) => (ISetup)sp.GetRequiredKeyedService<IPostDataStore>(key)
                );
            }

            services.AddScoped(sp => sp.GetRequiredKeyedService<IPostDataStore>(key));

            if (IsSetupType(type))
            {
                services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(key));
            }
        }

        services.AddScoped<IPostData, PostDataMultiStore>();
        return services;
    }

    public static IServiceCollection AddMediaDataInfrastructure(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalMediaData) };

        List<DataRegistration<StorageMedia>> registrations = ResolveRegistrations(
            services,
            GetAppConfig(services).Data.Media,
            types,
            keyOffset: 200
        );

        foreach (DataRegistration<StorageMedia> registration in registrations)
        {
            StorageMedia storage = registration.Storage;
            string key = registration.Key;
            Type type = registration.ImplementationType;

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    (IPartition)ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage)
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    LocalMediaCache instance = (LocalMediaCache)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalMediaCache),
                            storage,
                            partition
                        );

                    return instance;
                }
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);
                    LocalMediaCache cache = sp.GetRequiredKeyedService<LocalMediaCache>(key);

                    IMediaData instance = (IMediaData)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition, cache);

                    instance.Id = registration.Id;

                    return instance;
                }
            );

            if (IsSetupType(type))
            {
                services.AddKeyedScoped(
                    key,
                    (sp, _) => (ISetup)sp.GetRequiredKeyedService<IMediaData>(key)
                );
            }

            services.AddScoped<ISetup>(sp => sp.GetRequiredKeyedService<LocalMediaCache>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaData>(key));

            if (IsSetupType(type))
            {
                services.AddScoped(sp => sp.GetRequiredKeyedService<ISetup>(key));
            }
        }

        return services;
    }

    public static IServiceCollection AddMediaBackupInfrastructure(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(MediaBackup) };

        List<DataRegistration<StorageBackup>> registrations = ResolveRegistrations(
            services,
            GetAppConfig(services).Data.Backup,
            types,
            keyOffset: 400
        );

        foreach (DataRegistration<StorageBackup> registration in registrations)
        {
            StorageBackup storage = registration.Storage;
            string key = registration.Key;
            Type type = registration.ImplementationType;

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    (IPartition)ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage)
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    IMediaBackupData instance = (IMediaBackupData)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalMediaBackup),
                            storage,
                            partition
                        );

                    return instance;
                }
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IMediaBackupData mediaBackup = sp.GetRequiredKeyedService<IMediaBackupData>(key);

                    IMediaBackup instance = (IMediaBackup)
                        ActivatorUtilities.CreateInstance(sp, type, storage, mediaBackup);

                    instance.Id = registration.Id;

                    return instance;
                }
            );

            services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IMediaBackupData>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IMediaBackup>(key));
        }

        return services;
    }

    public static IServiceCollection AddBulkDataInfrastructure(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalBulkData) };

        List<DataRegistration<StorageBulk>> registrations = ResolveRegistrations(
            services,
            GetAppConfig(services).Data.Bulk,
            types,
            keyOffset: 100
        );

        foreach (DataRegistration<StorageBulk> registration in registrations)
        {
            StorageBulk storage = registration.Storage;
            string key = registration.Key;
            Type type = registration.ImplementationType;

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    (IPartition)ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage)
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    IBulkSourceDataStore instance = (IBulkSourceDataStore)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalBulkSourceData),
                            storage,
                            partition
                        );

                    instance.IsDefault = storage.Default;
                    return instance;
                }
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    IBulkDataStore instance = (IBulkDataStore)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = registration.Id;
                    instance.IsDefault = storage.Default;

                    return instance;
                }
            );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IBulkSourceDataStore>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IBulkDataStore>(key));

            if (typeof(ISetup).IsAssignableFrom(typeof(LocalBulkSourceData)))
            {
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IBulkSourceDataStore>(key));
            }

            if (IsSetupType(type))
            {
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IBulkDataStore>(key));
            }
        }

        services.AddScoped<IBulkSourceData, BulkSourceDataMultiStore>();
        services.AddScoped<IBulkData, BulkDataMultiStore>();
        return services;
    }

    public static IServiceCollection AddDumpDataInfrastructure(this IServiceCollection services)
    {
        Dictionary<string, Type> types = new() { ["local"] = typeof(LocalDumpData) };

        List<DataRegistration<StorageDump>> registrations = ResolveRegistrations(
            services,
            GetAppConfig(services).Data.Dump,
            types,
            keyOffset: 300
        );

        foreach (DataRegistration<StorageDump> registration in registrations)
        {
            StorageDump storage = registration.Storage;
            string key = registration.Key;
            Type type = registration.ImplementationType;

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                    (IPartition)ActivatorUtilities.CreateInstance(sp, typeof(LocalPartition), storage)
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    IDumpsDataStore instance = (IDumpsDataStore)
                        ActivatorUtilities.CreateInstance(
                            sp,
                            typeof(LocalDumpsData),
                            storage,
                            partition
                        );

                    instance.IsDefault = storage.Default;
                    return instance;
                }
            );

            services.AddKeyedScoped(
                key,
                (sp, _) =>
                {
                    IPartition partition = sp.GetRequiredKeyedService<IPartition>(key);

                    IDumpDataStore instance = (IDumpDataStore)
                        ActivatorUtilities.CreateInstance(sp, type, storage, partition);

                    instance.Id = registration.Id;
                    instance.IsDefault = storage.Default;

                    return instance;
                }
            );

            services.AddScoped(sp => sp.GetRequiredKeyedService<IDumpsDataStore>(key));
            services.AddScoped(sp => sp.GetRequiredKeyedService<IDumpDataStore>(key));

            if (typeof(ISetup).IsAssignableFrom(typeof(LocalDumpsData)))
            {
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IDumpsDataStore>(key));
            }

            if (IsSetupType(type))
            {
                services.AddScoped(sp => (ISetup)sp.GetRequiredKeyedService<IDumpDataStore>(key));
            }
        }

        services.AddScoped<IDumpsData, DumpsDataMultiStore>();
        services.AddScoped<IDumpData, DumpDataMultiStore>();
        return services;
    }

    private readonly record struct DataRegistration<TStorage>(
        string Key,
        string Id,
        TStorage Storage,
        Type ImplementationType
    )
        where TStorage : Storage;

    private static List<DataRegistration<TStorage>> ResolveRegistrations<TStorage>(
        IServiceCollection services,
        IEnumerable<TStorage> storages,
        IReadOnlyDictionary<string, Type> types,
        int keyOffset
    )
        where TStorage : Storage
    {
        List<TStorage> enabled = storages
            .Where(storage => storage.Enabled && types.ContainsKey(storage.Type))
            .ToList();

        int defaultCount = enabled.Count(storage => storage.Default);

        if (enabled.Count > 0 && defaultCount == 0)
        {
            throw new InvalidOperationException(
                "At least one enabled storage must have Default=true in the same data collection."
            );
        }

        if (defaultCount > 1)
        {
            throw new InvalidOperationException(
                "Only one enabled storage can have Default=true in the same data collection."
            );
        }

        List<DataRegistration<TStorage>> registrations = new(enabled.Count);

        for (int i = 0; i < enabled.Count; i++)
        {
            TStorage storage = enabled[i];
            string key = (i + keyOffset).ToString();
            storage.Id ??= key;

            registrations.Add(
                new DataRegistration<TStorage>(
                    Key: key,
                    Id: storage.Id,
                    Storage: storage,
                    ImplementationType: types[storage.Type]
                )
            );
        }

        return registrations;
    }

    private static bool IsSetupType(Type type) => typeof(ISetup).IsAssignableFrom(type);

    private static AppConfig GetAppConfig(IServiceCollection services)
    {
        ServiceDescriptor? descriptor = services.LastOrDefault(o => o.ServiceType == typeof(AppConfig));

        if (descriptor?.ImplementationInstance is AppConfig config)
        {
            return config;
        }

        throw new InvalidOperationException(
            $"{nameof(AppConfig)} is not registered as an implementation instance."
        );
    }
}
