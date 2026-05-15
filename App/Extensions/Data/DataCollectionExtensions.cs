using Backup.App.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

internal static class DataCollectionExtensions
{
    internal readonly record struct DataRegistration<TStorage>(
        string Key,
        string Id,
        TStorage Storage,
        Type ImplementationType
    )
        where TStorage : Models.Config.Data.Storage;

    public static List<DataRegistration<TStorage>> ResolveRegistrations<TStorage>(
        this IServiceCollection services,
        IEnumerable<TStorage> storages,
        IReadOnlyDictionary<string, Type> types,
        int keyOffset
    )
        where TStorage : Models.Config.Data.Storage
    {
        List<TStorage> enabled = storages
            .Where(storage => storage.Enabled && types.ContainsKey(storage.Type))
            .ToList();

        int defaultCount = enabled.Count(storage => storage.Default);

        if (enabled.Count > 0 && defaultCount == 0)
            throw new InvalidOperationException(
                "At least one enabled storage must have Default=true in the same data collection."
            );

        if (defaultCount > 1)
            throw new InvalidOperationException(
                "Only one enabled storage can have Default=true in the same data collection."
            );

        List<DataRegistration<TStorage>> registrations = new(enabled.Count);

        for (int i = 0; i < enabled.Count; i++)
        {
            TStorage storage = enabled[i];
            string key = (i + keyOffset).ToString();

            storage.Id ??= key;

            registrations.Add(
                new(
                    Key: key,
                    Id: storage.Id,
                    Storage: storage,
                    ImplementationType: types[storage.Type]
                )
            );
        }

        return registrations;
    }

    public static bool IsSetupType(this Type type) => typeof(ISetup).IsAssignableFrom(type);
}
