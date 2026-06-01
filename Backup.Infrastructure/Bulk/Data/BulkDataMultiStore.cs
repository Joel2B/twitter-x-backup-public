using Backup.Application.Core;
using Backup.Infrastructure.Bulk.Abstractions.Data;
using Backup.Infrastructure.Bulk.Models;

namespace Backup.Infrastructure.Bulk.Data;

public class BulkDataMultiStore(
    IEnumerable<IBulkDataStore> stores,
    IPrimarySelectionService primarySelectionService,
    ISecondaryStoreSelectionService secondaryStoreSelectionService
) : IBulkData
{
    private readonly List<IBulkDataStore> _stores = [.. stores];
    private readonly IPrimarySelectionService _primarySelectionService = primarySelectionService;
    private readonly ISecondaryStoreSelectionService _secondaryStoreSelectionService =
        secondaryStoreSelectionService;

    private IBulkDataStore Primary =>
        _primarySelectionService.ResolvePrimary(
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

    public Task<List<BulkData>?> GetBulks(CancellationToken cancellationToken = default) =>
        Primary.GetBulks(cancellationToken);

    public async Task Save(List<BulkData> bulks, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IBulkDataStore primary = Primary;
        await primary.Save(bulks, cancellationToken);

        foreach (
            IBulkDataStore store in _secondaryStoreSelectionService.SelectSecondaries(
                _stores,
                primary
            )
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            await store.Save(bulks, cancellationToken);
        }
    }

    public async Task Prune(CancellationToken cancellationToken = default)
    {
        foreach (IBulkDataStore store in _stores)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await store.Prune(cancellationToken);
        }
    }
}
