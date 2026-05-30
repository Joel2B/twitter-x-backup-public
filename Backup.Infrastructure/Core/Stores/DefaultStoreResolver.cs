using Backup.Infrastructure.Interfaces.Data;

namespace Backup.Infrastructure.Core.Stores;

public static class DefaultStoreResolver
{
    public static TStore ResolvePrimary<TStore>(
        IReadOnlyList<TStore> stores,
        string noStoresMessage,
        string multipleDefaultsMessage
    )
        where TStore : IDefaultStore
    {
        if (stores.Count == 0)
            throw new InvalidOperationException(noStoresMessage);

        List<TStore> defaults = stores.Where(store => store.IsDefault).ToList();

        if (defaults.Count > 1)
            throw new InvalidOperationException(multipleDefaultsMessage);

        return defaults.FirstOrDefault() ?? stores[0];
    }
}
