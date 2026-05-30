using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Application.Core;
using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Data;

public class BulkSourceDataMultiStore(
    IEnumerable<IBulkSourceDataStore> stores,
    IPrimarySelectionService primarySelectionService
) : IBulkSourceData
{
    private readonly List<IBulkSourceDataStore> _stores = [.. stores];
    private readonly IPrimarySelectionService _primarySelectionService = primarySelectionService;

    private IBulkSourceDataStore Primary
        => _primarySelectionService.ResolvePrimary(
            _stores,
            store => store.IsDefault,
            "No bulk source data stores are configured.",
            "Only one bulk source data store can be marked as default."
        );

    public Task<List<Source>> GetSources() => Primary.GetSources();
}
