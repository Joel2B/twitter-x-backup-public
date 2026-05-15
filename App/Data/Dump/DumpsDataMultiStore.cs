using Backup.App.Interfaces.Data.Post;
using Backup.App.Models.Dump;

namespace Backup.App.Data.Post;

public class DumpsDataMultiStore(IEnumerable<IDumpsDataStore> stores) : IDumpsData
{
    private readonly List<IDumpsDataStore> _stores = [.. stores];

    private IDumpsDataStore Primary
    {
        get
        {
            if (_stores.Count == 0)
                throw new InvalidOperationException("No dumps data stores are configured.");

            List<IDumpsDataStore> defaults = _stores.Where(store => store.IsDefault).ToList();

            if (defaults.Count > 1)
                throw new InvalidOperationException(
                    "Only one dumps data store can be marked as default."
                );

            return defaults.FirstOrDefault() ?? _stores.First();
        }
    }

    public Task<DumpsData> GetData() => Primary.GetData();

    public async Task Save(DumpsData dumps)
    {
        await Primary.Save(dumps);

        foreach (IDumpsDataStore store in _stores.Except([Primary]))
            await store.Save(dumps);
    }
}
