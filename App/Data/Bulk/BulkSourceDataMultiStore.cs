using Backup.App.Interfaces.Data.Bulk;

namespace Backup.App.Data.Bulk;

public class BulkSourceDataMultiStore(IEnumerable<IBulkSourceDataStore> stores) : IBulkSourceData
{
    private readonly List<IBulkSourceDataStore> _stores = [.. stores];

    private IBulkSourceDataStore Primary
    {
        get
        {
            if (_stores.Count == 0)
                throw new InvalidOperationException("No bulk source data stores are configured.");

            List<IBulkSourceDataStore> defaults = _stores.Where(store => store.IsDefault).ToList();

            if (defaults.Count > 1)
                throw new InvalidOperationException(
                    "Only one bulk source data store can be marked as default."
                );

            return defaults.FirstOrDefault() ?? _stores.First();
        }
    }

    public Task<List<Models.Bulk.Source>> GetSources() => Primary.GetSources();
}
