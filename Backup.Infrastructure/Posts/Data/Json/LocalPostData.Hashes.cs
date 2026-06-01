using Backup.Application.Posts.Models;
using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Infrastructure.Posts.Models;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Data.Json;

public partial class LocalPostData
{
    private async Task<Dictionary<string, PostMetaRow>> GetPostMetaCache()
    {
        if (_postMetaCache is not null)
            return _postMetaCache;

        PrepareTablesDirectories();
        string postMetaPath = GetCurrentTablesFilePath(PostMetaFileName);

        if (File.Exists(postMetaPath))
        {
            string content = await File.ReadAllTextAsync(postMetaPath);

            List<PostMetaRow> rows =
                JsonConvert.DeserializeObject<List<PostMetaRow>>(content) ?? [];
            IReadOnlyDictionary<string, PostMetaRecord> normalized =
                _postMetaNormalizationService.Normalize(
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

            return _postMetaCache;
        }

        _postMetaCache = [];
        return _postMetaCache;
    }

    private async Task<Dictionary<string, PostMetaRow>> EnsurePostMetaCache(IEnumerable<Post> posts)
    {
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

        List<PostMetaRecord> current = posts
            .Select(post => new PostMetaRecord
            {
                Id = post.Id,
                Hash = ComputePostHash(post),
                Deleted = post.Deleted,
            })
            .ToList();

        IReadOnlyDictionary<string, PostMetaRecord> reconciled =
            _postMetaReconciliationService.Reconcile(existing, current);

        return reconciled.ToDictionary(
            entry => entry.Key,
            entry => new PostMetaRow
            {
                Id = entry.Value.Id,
                Hash = entry.Value.Hash,
                Deleted = entry.Value.Deleted,
            },
            StringComparer.Ordinal
        );
    }

    private string ComputePostHash(Post post)
    {
        Backup.Domain.Posts.Post domainPost = PostReplicationMapper.ToDomain(post);
        return _postHashingService.Compute(domainPost);
    }
}
