using Backup.Application.Core;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Dump.Data;

public class DumpsDataMultiStore(
    IEnumerable<IDumpsDataStore> stores,
    IPrimarySelectionService primarySelectionService,
    ISecondaryStoreSelectionService secondaryStoreSelectionService
) : IDumpsData
{
    private readonly List<IDumpsDataStore> _stores = [.. stores];
    private readonly IPrimarySelectionService _primarySelectionService = primarySelectionService;
    private readonly ISecondaryStoreSelectionService _secondaryStoreSelectionService =
        secondaryStoreSelectionService;

    private IDumpsDataStore Primary =>
        _primarySelectionService.ResolvePrimary(
            _stores,
            store => store.IsDefault,
            "No dumps data stores are configured.",
            "Only one dumps data store can be marked as default."
        );

    public Task<DumpsData> GetData(CancellationToken cancellationToken = default) =>
        Primary.GetData(cancellationToken);

    public async Task Save(DumpsData dumps, CancellationToken cancellationToken = default)
    {
        IDumpsDataStore primary = Primary;
        await primary.Save(dumps, cancellationToken);

        foreach (
            IDumpsDataStore store in _secondaryStoreSelectionService.SelectSecondaries(
                _stores,
                primary
            )
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            await store.Save(dumps, cancellationToken);
        }
    }
}
