using Backup.Application.Posts.Ports;

namespace Backup.Application.Posts;

public class PostReplicationService : IPostReplicationService
{
    private const int ReplicationChunkSize = 10000;

    public async Task Replicate(IEnumerable<IPostReplicationStore> stores)
    {
        List<IPostReplicationStore> storeList = [.. stores];

        if (storeList.Count == 0)
            throw new InvalidOperationException(
                "No post data stores were provided for replication."
            );

        List<IPostReplicationStore> defaults = storeList.Where(store => store.IsDefault).ToList();

        if (defaults.Count > 1)
            throw new InvalidOperationException(
                "Only one post data store can be marked as default."
            );

        IPostReplicationStore source = defaults.FirstOrDefault() ?? storeList.First();
        Dictionary<string, string> sourceHashes = await source.GetHashesById();

        foreach (
            IPostReplicationStore target in storeList.Where(store =>
                !ReferenceEquals(store, source)
            )
        )
        {
            Dictionary<string, string> targetHashes = await target.GetHashesById();
            bool hasExtraIds = targetHashes.Keys.Any(id => !sourceHashes.ContainsKey(id));

            if (hasExtraIds)
            {
                List<Backup.Domain.Posts.Post>? allPosts = await source.GetAll();

                if (allPosts is null)
                    throw new InvalidOperationException(
                        "Replication source returned no posts for full reset."
                    );

                await target.Reset(allPosts);
                await target.Save();
                await target.Prune();
                continue;
            }

            List<string> changedIds = sourceHashes
                .Where(entry =>
                    !targetHashes.TryGetValue(entry.Key, out string? targetHash)
                    || !string.Equals(targetHash, entry.Value, StringComparison.Ordinal)
                )
                .Select(entry => entry.Key)
                .ToList();

            if (changedIds.Count == 0)
                continue;

            foreach (List<string> chunk in Chunk(changedIds, ReplicationChunkSize))
            {
                List<Backup.Domain.Posts.Post> changedPosts = await source.GetByIds(chunk);

                if (changedPosts.Count == 0)
                    continue;

                await target.UpsertPosts(changedPosts);
                await target.Save();
            }

            await target.Prune();
        }
    }

    private static IEnumerable<List<string>> Chunk(List<string> ids, int size)
    {
        if (size <= 0)
            throw new ArgumentOutOfRangeException(nameof(size));

        for (int i = 0; i < ids.Count; i += size)
        {
            int count = Math.Min(size, ids.Count - i);
            yield return ids.GetRange(i, count);
        }
    }
}
