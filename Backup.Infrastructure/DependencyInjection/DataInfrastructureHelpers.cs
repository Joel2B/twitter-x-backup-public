using Backup.App.Interfaces;
using Backup.App.Models.Config;
using Backup.App.Models.Config.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.Infrastructure.DependencyInjection;

internal static class DataInfrastructureHelpers
{
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
