using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Posts;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Services.Posts;

public class PostReplication(ILogger<PostReplication> _logger) : IPostReplication
{
    private readonly ILogger<PostReplication> _logger = _logger;
    private const int ReplicationChunkSize = 10000;

    public async Task Replicate(IEnumerable<IPostDataStore> stores)
    {
        List<IPostDataStore> storeList = [.. stores];

        if (storeList.Count == 0)
            throw new InvalidOperationException(
                "No post data stores were provided for replication."
            );

        List<IPostDataStore> defaults = storeList.Where(store => store.IsDefault).ToList();

        if (defaults.Count > 1)
            throw new InvalidOperationException(
                "Only one post data store can be marked as default."
            );

        IPostDataStore source = defaults.First();

        try
        {
            Dictionary<string, string> sourceHashes = await source.GetHashesById();
            _logger.LogInformation("replication source hashes: {count}", sourceHashes.Count);

            foreach (
                IPostDataStore postData in storeList.Where(store => !ReferenceEquals(store, source))
            )
            {
                Dictionary<string, string> targetHashes = await postData.GetHashesById();

                _logger.LogInformation(
                    "replication target '{target}' hashes: {count}",
                    postData.Id ?? postData.GetType().Name,
                    targetHashes.Count
                );

                bool hasExtraIds = targetHashes.Keys.Any(id => !sourceHashes.ContainsKey(id));

                if (hasExtraIds)
                {
                    _logger.LogWarning(
                        "target '{target}' has ids not present in source; falling back to full reset",
                        postData.Id ?? postData.GetType().Name
                    );

                    List<Post>? allPosts = await source.GetAll();

                    if (allPosts is null)
                        throw new Exception("replication source returned no posts for full reset");

                    await postData.Reset(allPosts);
                    await postData.Save();
                    await postData.Prune();
                    continue;
                }

                List<string> changedIds = sourceHashes
                    .Where(entry =>
                        !targetHashes.TryGetValue(entry.Key, out string? targetHash)
                        || !string.Equals(targetHash, entry.Value, StringComparison.Ordinal)
                    )
                    .Select(entry => entry.Key)
                    .ToList();

                _logger.LogInformation(
                    "replication target '{target}' changed ids: {count}",
                    postData.Id ?? postData.GetType().Name,
                    changedIds.Count
                );

                if (changedIds.Count == 0)
                    continue;

                int processed = 0;

                foreach (List<string> chunk in Chunk(changedIds, ReplicationChunkSize))
                {
                    List<Post> changedPosts = await source.GetByIds(chunk);

                    if (changedPosts.Count == 0)
                    {
                        processed += chunk.Count;
                        continue;
                    }

                    await postData.UpsertPosts(changedPosts);
                    await postData.Save();
                    processed += chunk.Count;

                    _logger.LogInformation(
                        "replication target '{target}' progress: {processed}/{total}",
                        postData.Id ?? postData.GetType().Name,
                        processed,
                        changedIds.Count
                    );
                }

                await postData.Prune();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", JsonConvert.SerializeObject(ex));
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


