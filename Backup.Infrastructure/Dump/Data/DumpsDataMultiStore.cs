using Backup.Application.Core;
using Backup.Infrastructure.Core.Data;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Dump.Data;

public class DumpsDataMultiStore(
    IEnumerable<IDumpsDataStore> stores,
    IPrimarySelectionService primarySelectionService,
    ISecondaryStoreSelectionService secondaryStoreSelectionService
) : IDumpsData
{
    private readonly DefaultStoreGroup<IDumpsDataStore> _storeGroup = new(
        stores,
        primarySelectionService,
        secondaryStoreSelectionService,
        "No dumps data stores are configured.",
        "Only one dumps data store can be marked as default."
    );

    private IDumpsDataStore Primary => _storeGroup.Primary;

    public Task<DumpsData> GetData(CancellationToken cancellationToken = default) =>
        Primary.GetData(cancellationToken);

    public async Task Save(DumpsData dumps, CancellationToken cancellationToken = default)
    {
        IDumpsDataStore primary = Primary;
        await primary.Save(dumps, cancellationToken);

        foreach (IDumpsDataStore store in _storeGroup.GetSecondaries(primary))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await store.Save(dumps, cancellationToken);
        }
    }
}
