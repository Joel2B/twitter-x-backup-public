using Backup.Application.Posts.Models;
using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models.Stored;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Data.Json;

public partial class LocalPostData
{
    private async Task<Dictionary<string, PostMetaRow>> GetPostMetaCache()
    {
        if (_postMetaCache is not null)
        {
            _logger.LogInformation(
                "post-meta: using in-memory cache with {count} rows",
                _postMetaCache.Count
            );

            return _postMetaCache;
        }

        _logger.LogInformation("post-meta: preparing directories");
        PrepareTablesDirectories();
        string postMetaPath = GetCurrentTablesFilePath(PostMetaFileName);

        if (File.Exists(postMetaPath))
        {
            _logger.LogInformation("post-meta: reading post_meta.json");
            string content = await File.ReadAllTextAsync(postMetaPath);

            List<PostMetaRow> rows =
                JsonConvert.DeserializeObject<List<PostMetaRow>>(content) ?? [];

            IReadOnlyDictionary<string, PostMetaRecord> normalized = _hashCoordinator.Normalize(
                rows.Select(row => new PostMetaRecord
                    {
                        Id = row.Id,
                        Hash = row.Hash,
                        Deleted = row.Deleted,
                    })
                    .ToList()
            );

            _postMetaCache = normalized.ToDictionary(
                entry => entry.Key,
                entry => new PostMetaRow
                {
                    Id = entry.Value.Id,
                    Hash = entry.Value.Hash,
                    Deleted = entry.Value.Deleted,
                },
                StringComparer.Ordinal
            );
            _logger.LogInformation("post-meta: loaded {count} rows", _postMetaCache.Count);

            return _postMetaCache;
        }

        _postMetaCache = [];
        _logger.LogInformation("post-meta: no file found, initialized empty cache");

        return _postMetaCache;
    }

    private async Task<Dictionary<string, PostMetaRow>> EnsurePostMetaCache(IEnumerable<Post> posts)
    {
        List<Post> postList = posts as List<Post> ?? [.. posts];
        _logger.LogInformation("post-meta: reconciling hashes for {count} posts", postList.Count);

        Dictionary<string, PostMetaRow> meta = await GetPostMetaCache();
        IReadOnlyDictionary<string, PostMetaRecord> existing = meta.ToDictionary(
            entry => entry.Key,
            entry => new PostMetaRecord
            {
                Id = entry.Value.Id,
                Hash = entry.Value.Hash,
                Deleted = entry.Value.Deleted,
            },
            StringComparer.Ordinal
        );

        List<PostMetaRecord> current = postList
            .Select(post => new PostMetaRecord
            {
                Id = post.Id,
                Hash = ComputePostHash(post),
                Deleted = post.Deleted,
            })
            .ToList();

        IReadOnlyDictionary<string, PostMetaRecord> reconciled = _hashCoordinator.Reconcile(
            existing,
            current
        );

        Dictionary<string, PostMetaRow> result = reconciled.ToDictionary(
            entry => entry.Key,
            entry => new PostMetaRow
            {
                Id = entry.Value.Id,
                Hash = entry.Value.Hash,
                Deleted = entry.Value.Deleted,
            },
            StringComparer.Ordinal
        );

        _logger.LogInformation("post-meta: reconciliation completed, rows={count}", result.Count);
        return result;
    }

    private string ComputePostHash(Post post)
    {
        Backup.Domain.Posts.Post domainPost = PostReplicationMapper.ToDomain(post);
        return _hashCoordinator.ComputeHash(domainPost);
    }
}
