using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Post;
using Microsoft.Extensions.Logging;

namespace Backup.App.Services.Post;

public class PostReplication(ILogger<PostReplication> _logger) : IPostReplication
{
    private readonly ILogger<PostReplication> _logger = _logger;

    public async Task Replicate(IEnumerable<IPostData> data)
    {
        IPostData source = data.First();

        try
        {
            Dictionary<string, string> sourceHashes = await source.GetHashesById();
            _logger.LogInformation("replication source hashes: {count}", sourceHashes.Count);

            foreach (IPostData postData in data.Except([source]))
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

                    List<Models.Post.Post>? allPosts = await source.GetAll();

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

                List<Models.Post.Post> changedPosts = await source.GetByIds(changedIds);

                if (changedPosts.Count == 0)
                    continue;

                await postData.UpsertPosts(changedPosts);
                await postData.Save();
                await postData.Prune();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("Error: {error}", ex.Message);
        }
    }
}
