using Backup.Infrastructure.Interfaces.Data.Bulk;
using Backup.Infrastructure.Core.Stores;
using Backup.Infrastructure.Models.Bulk;

namespace Backup.Infrastructure.Bulk.Data;

public class BulkSourceDataMultiStore(IEnumerable<IBulkSourceDataStore> stores) : IBulkSourceData
{
    private readonly List<IBulkSourceDataStore> _stores = [.. stores];

    private IBulkSourceDataStore Primary
        => DefaultStoreResolver.ResolvePrimary(
            _stores,
            "No bulk source data stores are configured.",
            "Only one bulk source data store can be marked as default."
        );

    public Task<List<Source>> GetSources() => Primary.GetSources();
}


