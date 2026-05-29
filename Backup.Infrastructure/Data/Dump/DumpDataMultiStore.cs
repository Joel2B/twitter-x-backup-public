using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Models.Posts;

namespace Backup.Infrastructure.Data.Posts;

public class DumpDataMultiStore(IEnumerable<IDumpDataStore> stores) : IDumpData
{
    private readonly List<IDumpDataStore> _stores = [.. stores];

    private IDumpDataStore Primary
    {
        get
        {
            if (_stores.Count == 0)
                throw new InvalidOperationException("No dump data stores are configured.");

            List<IDumpDataStore> defaults = _stores.Where(store => store.IsDefault).ToList();

            if (defaults.Count > 1)
                throw new InvalidOperationException(
                    "Only one dump data store can be marked as default."
                );

            return defaults.FirstOrDefault() ?? _stores.First();
        }
    }

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

    public Task Flush(IPostData postData, string userId, ApiContext context) =>
        Primary.Flush(postData, userId, context);
}


