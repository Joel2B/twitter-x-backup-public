using Backup.App.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Backup.App.Extensions;

internal static class DataCollectionExtensions
{
    internal readonly record struct DataRegistration<TStorage>(
        int Index,
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
            .Where(o => o.Enabled && types.ContainsKey(o.Type))
            .ToList();

        List<DataRegistration<TStorage>> registrations = new(enabled.Count);

        for (int i = 0; i < enabled.Count; i++)
        {
            TStorage storage = enabled[i];
            string key = (i + keyOffset).ToString();

            storage.Id ??= key;

            registrations.Add(
                new(
                    Index: i,
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
