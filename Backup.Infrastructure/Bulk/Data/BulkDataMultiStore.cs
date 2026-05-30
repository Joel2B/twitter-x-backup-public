using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Core.Stores;
using Backup.Infrastructure.Models.Bulk;

namespace Backup.Infrastructure.Bulk.Data;

public class BulkDataMultiStore(IEnumerable<IBulkDataStore> stores) : IBulkData
{
    private readonly List<IBulkDataStore> _stores = [.. stores];

    private IBulkDataStore Primary
        => DefaultStoreResolver.ResolvePrimary(
            _stores,
            "No bulk data stores are configured.",
            "Only one bulk data store can be marked as default."
        );

    public string? Id
    {
        get => Primary.Id;
        set => Primary.Id = value;
    }

    public Task<List<BulkData>?> GetBulks() => Primary.GetBulks();

    public async Task Save(List<BulkData> bulks)
    {
        await Primary.Save(bulks);

        foreach (IBulkDataStore store in _stores.Except([Primary]))
            await store.Save(bulks);
    }

    public async Task Prune()
    {
        foreach (IBulkDataStore store in _stores)
            await store.Prune();
    }
}
