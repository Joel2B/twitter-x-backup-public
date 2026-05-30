using Backup.Infrastructure.Models.Data.Json;
using Backup.Infrastructure.Models.Posts;
using Newtonsoft.Json;

namespace Backup.Infrastructure.Posts.Data;

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

            _postMetaCache = rows.Where(row =>
                    !string.IsNullOrWhiteSpace(row.Id) && !string.IsNullOrWhiteSpace(row.Hash)
                )
                .GroupBy(row => row.Id, StringComparer.Ordinal)
                .Select(group => group.Last())
                .ToDictionary(row => row.Id, row => row, StringComparer.Ordinal);

            return _postMetaCache;
        }

        _postMetaCache = [];
        return _postMetaCache;
    }

    private async Task<Dictionary<string, PostMetaRow>> EnsurePostMetaCache(IEnumerable<Post> posts)
    {
        Dictionary<string, PostMetaRow> meta = await GetPostMetaCache();
        HashSet<string> ids = [];

        foreach (Post post in posts)
        {
            ids.Add(post.Id);
            string hash = ComputePostHash(post);

            if (!meta.TryGetValue(post.Id, out PostMetaRow? value))
            {
                meta[post.Id] = new()
                {
                    Id = post.Id,
                    Hash = hash,
                    Deleted = post.Deleted,
                };

                continue;
            }

            value.Deleted = post.Deleted;
            value.Hash = hash;
        }

        List<string> staleIds = [.. meta.Keys.Where(id => !ids.Contains(id))];

        foreach (string staleId in staleIds)
            meta.Remove(staleId);

        return meta;
    }

    private static string ComputePostHash(Post post)
    {
        return Backup.Infrastructure.Utils.PostHash.Compute(post);
    }
}


