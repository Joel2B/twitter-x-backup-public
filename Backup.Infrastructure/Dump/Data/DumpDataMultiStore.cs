using Backup.Infrastructure.Core.Stores;
using Backup.Infrastructure.Interfaces.Data.Dump;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Models.Posts;

namespace Backup.Infrastructure.Dump.Data;

public class DumpDataMultiStore(IEnumerable<IDumpDataStore> stores) : IDumpData
{
    private readonly List<IDumpDataStore> _stores = [.. stores];

    private IDumpDataStore Primary
        => DefaultStoreResolver.ResolvePrimary(
            _stores,
            "No dump data stores are configured.",
            "Only one dump data store can be marked as default."
        );

    public string? Id
    {
        get => Primary.Id;
        set => Primary.Id = value;
    }

    public Task<DumpData?> GetData(ApiContext context) => Primary.GetData(context);

    public async Task Save(string response, List<Post> posts, string cursor, ApiContext context)
    {
        await Primary.Save(response, posts, cursor, context);

        foreach (IDumpDataStore store in _stores.Except([Primary]))
            await store.Save(response, posts, cursor, context);
    }

    public Task Flush(IPostDomainData postData, string userId, ApiContext context) =>
        Primary.Flush(postData, userId, context);
}


