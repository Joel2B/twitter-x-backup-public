using Backup.Application.Core;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Models;
using Backup.Infrastructure.Core.Data;

namespace Backup.Infrastructure.Bulk.Data;

public class BulkDataMultiStore(
    IEnumerable<IBulkDataStore> stores,
    IPrimarySelectionService primarySelectionService,
    ISecondaryStoreSelectionService secondaryStoreSelectionService
) : IBulkData
{
    private readonly DefaultStoreGroup<IBulkDataStore> _storeGroup = new(
        stores,
        primarySelectionService,
        secondaryStoreSelectionService,
        "No bulk data stores are configured.",
        "Only one bulk data store can be marked as default."
    );

    private bool HasStores => _storeGroup.Stores.Count > 0;
    private IBulkDataStore Primary => _storeGroup.Primary;

    public string? Id
    {
        get => HasStores ? Primary.Id : null;
        set
        {
            if (!HasStores)
                return;

            Primary.Id = value;
        }
    }

    public Task<List<BulkData>?> GetBulks(CancellationToken cancellationToken = default) =>
        HasStores ? Primary.GetBulks(cancellationToken) : Task.FromResult<List<BulkData>?>(null);

    public async Task Save(List<BulkData> bulks, CancellationToken cancellationToken = default)
    {
        if (!HasStores)
            return;

        cancellationToken.ThrowIfCancellationRequested();
        IBulkDataStore primary = Primary;
        await primary.Save(bulks, cancellationToken);

        foreach (IBulkDataStore store in _storeGroup.GetSecondaries(primary))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await store.Save(bulks, cancellationToken);
        }
    }

    public async Task Prune(CancellationToken cancellationToken = default)
    {
        if (!HasStores)
            return;

        foreach (IBulkDataStore store in _storeGroup.Stores)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await store.Prune(cancellationToken);
        }
    }
}
