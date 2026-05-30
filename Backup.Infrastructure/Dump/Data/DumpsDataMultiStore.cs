using Backup.Infrastructure.Core.Stores;
using Backup.Infrastructure.Interfaces.Data.Dump;
using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Dump.Data;

public class DumpsDataMultiStore(IEnumerable<IDumpsDataStore> stores) : IDumpsData
{
    private readonly List<IDumpsDataStore> _stores = [.. stores];

    private IDumpsDataStore Primary
        => DefaultStoreResolver.ResolvePrimary(
            _stores,
            "No dumps data stores are configured.",
            "Only one dumps data store can be marked as default."
        );

    public Task<DumpsData> GetData() => Primary.GetData();

    public async Task Save(DumpsData dumps)
    {
        await Primary.Save(dumps);

        foreach (IDumpsDataStore store in _stores.Except([Primary]))
            await store.Save(dumps);
    }
}


