using Backup.Application.Posts.Ports;
using Microsoft.Extensions.Logging;

namespace Backup.Application.Posts;

public class PostReplicationService(ILogger<PostReplicationService> logger)
    : IPostReplicationService
{
    private const int ReplicationChunkSize = 10000;
    private readonly ILogger<PostReplicationService> _logger = logger;

    public async Task Replicate(IEnumerable<IPostReplicationStore> stores)
    {
        List<IPostReplicationStore> storeList = [.. stores];

        if (storeList.Count == 0)
            throw new InvalidOperationException(
                "No post data stores were provided for replication."
            );

        _logger.LogInformation("post replication started: stores={storeCount}", storeList.Count);

        List<IPostReplicationStore> defaults = storeList.Where(store => store.IsDefault).ToList();

        if (defaults.Count > 1)
            throw new InvalidOperationException(
                "Only one post data store can be marked as default."
            );

        IPostReplicationStore source = defaults.FirstOrDefault() ?? storeList.First();
        Dictionary<string, string> sourceHashes = await source.GetHashesById();
        int targetCount = storeList.Count - 1;

        _logger.LogInformation(
            "post replication source selected: source={sourceId}, default={isDefault}, hashes={hashCount}, targets={targetCount}",
            source.Id,
            source.IsDefault,
            sourceHashes.Count,
            targetCount
        );

        foreach (
            IPostReplicationStore target in storeList.Where(store =>
                !ReferenceEquals(store, source)
            )
        )
        {
            _logger.LogInformation("post replication target started: target={targetId}", target.Id);

            Dictionary<string, string> targetHashes = await target.GetHashesById();
            List<string> extraIds = targetHashes
                .Keys.Where(id => !sourceHashes.ContainsKey(id))
                .ToList();

            if (extraIds.Count > 0)
                _logger.LogInformation(
                    "post replication target has extra posts: target={targetId}, extraPosts={extraCount}, sample=[{extraSample}]",
                    target.Id,
                    extraIds.Count,
                    string.Join(", ", extraIds.Take(10))
                );

            List<string> changedIds = sourceHashes
                .Where(entry =>
                    !targetHashes.TryGetValue(entry.Key, out string? targetHash)
                    || !string.Equals(targetHash, entry.Value, StringComparison.Ordinal)
                )
                .Select(entry => entry.Key)
                .ToList();

            if (changedIds.Count == 0)
            {
                if (extraIds.Count > 0)
                {
                    foreach (List<string> chunk in Chunk(extraIds, ReplicationChunkSize))
                        await target.DeletePosts(chunk);

                    await target.Save();
                    await target.Prune();

                    _logger.LogInformation(
                        "post replication target completed: target={targetId}, changedPosts=0, removedPosts={removedCount}",
                        target.Id,
                        extraIds.Count
                    );

                    continue;
                }

                _logger.LogInformation(
                    "post replication target skipped: target={targetId}, no changes detected",
                    target.Id
                );
                continue;
            }

            int totalChunks = (int)Math.Ceiling(changedIds.Count / (double)ReplicationChunkSize);
            int chunkIndex = 0;

            _logger.LogInformation(
                "post replication target changes detected: target={targetId}, changedPosts={changedCount}, chunks={chunkCount}",
                target.Id,
                changedIds.Count,
                totalChunks
            );

            foreach (List<string> chunk in Chunk(changedIds, ReplicationChunkSize))
            {
                chunkIndex++;

                _logger.LogInformation(
                    "post replication target chunk started: target={targetId}, chunk={chunkIndex}/{totalChunks}, ids={idCount}",
                    target.Id,
                    chunkIndex,
                    totalChunks,
                    chunk.Count
                );

                List<Backup.Domain.Posts.Post> changedPosts = await source.GetByIds(chunk);
                ValidateSourceChunk(chunk, changedPosts);

                await target.UpsertPosts(changedPosts);
                await target.Save();

                _logger.LogInformation(
                    "post replication target chunk completed: target={targetId}, chunk={chunkIndex}/{totalChunks}, posts={postCount}",
                    target.Id,
                    chunkIndex,
                    totalChunks,
                    changedPosts.Count
                );
            }

            foreach (List<string> chunk in Chunk(extraIds, ReplicationChunkSize))
                await target.DeletePosts(chunk);

            if (extraIds.Count > 0)
                await target.Save();

            await target.Prune();

            _logger.LogInformation(
                "post replication target completed: target={targetId}, changedPosts={changedCount}, removedPosts={removedCount}",
                target.Id,
                changedIds.Count,
                extraIds.Count
            );
        }

        _logger.LogInformation("post replication completed");
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

    private static void ValidateSourceChunk(
        IReadOnlyCollection<string> requestedIds,
        IReadOnlyCollection<Backup.Domain.Posts.Post> posts
    )
    {
        HashSet<string> requested = requestedIds.ToHashSet(StringComparer.Ordinal);
        HashSet<string> returned = posts.Select(post => post.Id).ToHashSet(StringComparer.Ordinal);

        if (posts.Count != requested.Count || !returned.SetEquals(requested))
            throw new InvalidOperationException(
                $"Replication source returned an invalid post set: requested={requested.Count}, returned={posts.Count}, uniqueReturned={returned.Count}."
            );
    }
}
