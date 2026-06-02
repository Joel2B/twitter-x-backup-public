using Backup.Application.Core;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Models;
using Backup.Infrastructure.Core.Data;

namespace Backup.Infrastructure.Bulk.Data;

public class BulkSourceDataMultiStore(
    IEnumerable<IBulkSourceDataStore> stores,
    IPrimarySelectionService primarySelectionService
) : IBulkSourceData
{
    private readonly DefaultStoreGroup<IBulkSourceDataStore> _storeGroup = new(
        stores,
        primarySelectionService,
        null,
        "No bulk source data stores are configured.",
        "Only one bulk source data store can be marked as default."
    );

    private IBulkSourceDataStore Primary => _storeGroup.Primary;

    public Task<List<Source>> GetSources() => Primary.GetSources();
}
