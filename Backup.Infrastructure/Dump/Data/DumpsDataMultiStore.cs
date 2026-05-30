using Backup.Application.Core;
using Backup.Infrastructure.Dump.Abstractions.Data;
using Backup.Infrastructure.Models.Dump;

namespace Backup.Infrastructure.Dump.Data;

public class DumpsDataMultiStore(
    IEnumerable<IDumpsDataStore> stores,
    IPrimarySelectionService primarySelectionService
) : IDumpsData
{
    private readonly List<IDumpsDataStore> _stores = [.. stores];
    private readonly IPrimarySelectionService _primarySelectionService = primarySelectionService;

    private IDumpsDataStore Primary
        => _primarySelectionService.ResolvePrimary(
            _stores,
            store => store.IsDefault,
            "No dumps data stores are configured.",
            "Only one dumps data store can be marked as default."
        );

    public Task<DumpsData> GetData() => Primary.GetData();

    public async Task Save(DumpsData dumps)
    {
        await Primary.Save(dumps);

        foreach (IDumpsDataStore store in _stores.Except([Primary]))
            await store.Save(dumps);
    }
}
