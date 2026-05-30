using Backup.Infrastructure.Core.Abstractions.Setup;
using Backup.Application.Core;
using Backup.Infrastructure.Models.Config;
using Backup.Infrastructure.Models.Config.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection.Base;

internal static class DataInfrastructureHelpers
{
    private static readonly IStorageRegistrationPolicyService StorageRegistrationPolicy =
        new StorageRegistrationPolicyService();

    internal readonly record struct DataRegistration<TStorage>(
        string Key,
        string Id,
        TStorage Storage,
        Type ImplementationType
    )
        where TStorage : Storage;

    internal static List<DataRegistration<TStorage>> ResolveRegistrations<TStorage>(
        IServiceCollection services,
        IEnumerable<TStorage> storages,
        IReadOnlyDictionary<string, Type> types,
        int keyOffset
    )
        where TStorage : Storage
    {
        IReadOnlyList<TStorage> enabled = StorageRegistrationPolicy.SelectEnabled(
            storages,
            storage => storage.Enabled,
            storage => types.ContainsKey(storage.Type),
            storage => storage.Default
        );

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

    internal static bool IsSetupType(Type type) => typeof(ISetup).IsAssignableFrom(type);

    internal static AppConfig GetAppConfig(IServiceCollection services)
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
