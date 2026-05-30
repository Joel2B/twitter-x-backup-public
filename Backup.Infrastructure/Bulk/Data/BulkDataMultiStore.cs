using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Application.Core;
using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Data;

public class BulkDataMultiStore(
    IEnumerable<IBulkDataStore> stores,
    IPrimarySelectionService primarySelectionService
) : IBulkData
{
    private readonly List<IBulkDataStore> _stores = [.. stores];
    private readonly IPrimarySelectionService _primarySelectionService = primarySelectionService;

    private IBulkDataStore Primary
        => _primarySelectionService.ResolvePrimary(
            _stores,
            store => store.IsDefault,
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
