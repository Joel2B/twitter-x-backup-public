using Backup.App.Interfaces.Data.Bulk;
using Backup.App.Models.Bulk;

namespace Backup.App.Data.Bulk;

public class BulkDataMultiStore(IEnumerable<IBulkDataStore> stores) : IBulkData
{
    private readonly List<IBulkDataStore> _stores = [.. stores];

    private IBulkDataStore Primary
    {
        get
        {
            if (_stores.Count == 0)
                throw new InvalidOperationException("No bulk data stores are configured.");

            List<IBulkDataStore> defaults = _stores.Where(store => store.IsDefault).ToList();

            if (defaults.Count > 1)
                throw new InvalidOperationException(
                    "Only one bulk data store can be marked as default."
                );

            return defaults.FirstOrDefault() ?? _stores.First();
        }
    }

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
